using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace IngresosSPGGApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MultasController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly ILogger<MultasController> _logger;

    public MultasController(IDbConnection db, ILogger<MultasController> logger)
    {
        _db = db;
        _logger = logger;
    }

    //Obtener una lista de todas las multas
    [HttpGet("{placa}")]
    public async Task<IActionResult> GetByPlaca(string placa)
    {
        const string sql = @"
            SELECT  m.id_multa,
                    p.placa,
                    tm.titulo       AS tipo_multa,
                    -- Monto: miles con coma y 2 decimales con punto
                    FORMAT(m.monto, 'N2', 'en-US')                                       AS monto,
                    m.direccion,
                    -- Fechas: dd-MM-yyyy HH:mm:ss  (ej. 09-07-2025 15:42:00)
                    FORMAT(CAST(m.fecha_expedida AS datetime), 'dd-MM-yyyy HH:mm:ss')    AS fecha_expedida,
                    FORMAT(CAST(m.fecha_limite   AS datetime), 'dd-MM-yyyy HH:mm:ss')    AS fecha_limite,
                    m.latitude,
                    m.longitude
            FROM dbo.Multas        m
            JOIN dbo.PlacasAutos   p  ON p.id_placa = m.id_placa
            JOIN dbo.TiposMulta    tm ON tm.id_tipo_multa = m.id_tipo_multa
            WHERE p.placa = @placa
            AND m.pagado = 0
            ORDER BY m.fecha_expedida DESC;
        ";


        var multas = await _db.QueryAsync(sql, new { placa });
        return Ok(multas);
    }


    public record PagarMultaDto(int IdMulta, decimal MontoPagar);

    //Pagar multa de manera individual
    [HttpPost("pagar")]
    public async Task<IActionResult> PagarMulta([FromBody] PagarMultaDto dto)
    {

        _logger.LogInformation("Pago individual multa: "+dto.IdMulta+" Monto: "+dto.MontoPagar);

        const string selectSql = @"
            SELECT id_multa, monto, fecha_limite, fecha_pago
            FROM dbo.Multas
            WHERE id_multa = @IdMulta 
            AND pagado = 0";

        var multa = await _db.QuerySingleOrDefaultAsync(selectSql, new { dto.IdMulta });

        if (multa is null)
            return NotFound($"Multa {dto.IdMulta} no existe.");

        if (multa.pagado == 1)
            return BadRequest("La multa ya fue pagada anteriormente.");

        if (multa.monto != dto.MontoPagar)
            return BadRequest("El monto a pagar no coincide con el importe de la multa. Por pagar: " + multa.monto + " intento: " + dto.MontoPagar);

        bool aTiempo = DateTime.UtcNow.Date <= ((DateTime)multa.fecha_limite).Date;

        const string updateSql = @"
            UPDATE dbo.Multas
            SET   fecha_pago    = @FechaHoy,
                pago_a_tiempo = @ATiempo,
                pagado = 1
            WHERE id_multa      = @IdMulta";

        await _db.ExecuteAsync(updateSql, new
        {
            FechaHoy = DateTime.UtcNow,
            ATiempo = aTiempo,
            dto.IdMulta
        });

        return Ok(new
        {
            mensaje = "Multa pagada correctamente",
            id_multa = dto.IdMulta,
            fecha_pago = DateTime.UtcNow,
            pago_a_tiempo = aTiempo
        });
    }


    //Consultar el total de la deuda por multas con la placa.
    [HttpGet("total/{placa}")]
    public async Task<IActionResult> GetDeudaByPlaca(string placa)
    {
        const string sql = @"
        SELECT SUM(m.monto) AS TotalAdeudo
        FROM   dbo.Multas m
        INNER  JOIN dbo.PlacasAutos p ON p.id_placa = m.id_placa
        WHERE  p.placa   = @placa
          AND  m.pagado  = 0;";

        var total = await _db.ExecuteScalarAsync<decimal?>(sql, new { placa });

        // Si no hay multas pendientes devolvemos 0.
        return Ok(new
        {
            placa,
            total_a_pagar = total ?? 0m
        });
    }

    public record PagarTotalDto(string Placa, decimal MontoPagar);
    
    //Pagar multas de manera total con la placa.
    [HttpPost("pagar-total")]
    public async Task<IActionResult> PagarTotal([FromBody] PagarTotalDto dto)
    {

        _logger.LogInformation("Pago todas multa: "+dto.Placa+" Monto: "+dto.MontoPagar);

        const string sqlTotal = @"
            SELECT SUM(m.monto) AS TotalPendiente
            FROM   dbo.Multas m
            JOIN   dbo.PlacasAutos p ON p.id_placa = m.id_placa
            WHERE  p.placa  = @Placa
            AND  m.pagado = 0;";

        var totalPendiente = await _db.ExecuteScalarAsync<decimal?>(sqlTotal, dto);

        if (totalPendiente is null || totalPendiente == 0)
            return BadRequest("No hay multas pendientes para esta placa.");

        if (totalPendiente != dto.MontoPagar)
            return BadRequest($"Monto incorrecto. Total pendiente: {totalPendiente:0.00}");

        var fechaPago = DateTime.UtcNow;

        const string sqlUpdate = @"
            UPDATE m
            SET    m.fecha_pago    = @FechaPago,
                m.pago_a_tiempo = CASE WHEN @FechaPago <= m.fecha_limite THEN 1 ELSE 0 END,
                m.pagado        = 1
            FROM   dbo.Multas m
            JOIN   dbo.PlacasAutos p ON p.id_placa = m.id_placa
            WHERE  p.placa  = @Placa
            AND  m.pagado = 0;";

        if (_db.State == ConnectionState.Closed)
        _db.Open();

        using var tx = _db.BeginTransaction();
        var filas = await _db.ExecuteAsync(sqlUpdate,
                new { dto.Placa, FechaPago = fechaPago }, tx);
        tx.Commit();

        return Ok(new
        {
            placa          = dto.Placa,
            total_pagado   = totalPendiente,
            multas_pagadas = filas,
            fecha_pago     = fechaPago
        });
    }

}
