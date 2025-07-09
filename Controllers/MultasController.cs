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
        _db     = db;
        _logger = logger;
    }

    [HttpGet("{placa}")]
    public async Task<IActionResult> GetByPlaca(string placa)
    {
        const string sql = @"
            SELECT  m.id_multa,
                    p.placa,
                    tm.titulo       AS tipo_multa,
                    m.monto,
                    m.direccion,
                    m.fecha_expedida,
                    m.fecha_limite,
                    m.fecha_pago,
                    m.pago_a_tiempo
            FROM dbo.Multas        m
            JOIN dbo.PlacasAutos   p  ON p.id_placa = m.id_placa
            JOIN dbo.TiposMulta    tm ON tm.id_tipo_multa = m.id_tipo_multa
            WHERE p.placa = @placa
            ORDER BY m.fecha_expedida DESC;
        ";


        var multas = await _db.QueryAsync(sql, new { placa });
        return Ok(multas);
    }
}
