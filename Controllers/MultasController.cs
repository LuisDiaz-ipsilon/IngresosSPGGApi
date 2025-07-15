using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using System.Text;  
using System.Net;
using System.Net.Mail;   
using QuestPDF.Infrastructure;

namespace IngresosSPGGApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MultasController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly ILogger<MultasController> _logger;

   private readonly IConfiguration _configuration;

    public MultasController(IDbConnection db, ILogger<MultasController> logger, IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    // Obtener PDF por placa para pagar multas
    [HttpGet("pdf/{placa}")]
    public async Task<IActionResult> GetPdfByPlaca(string placa)
    {
        try
        {
            // Consultar multas
            const string sqlMultas = @"
                SELECT  m.id_multa,
                        p.placa,
                        p.titular,
                        tm.titulo       AS tipo_multa,
                        m.monto,
                        m.direccion,
                        m.detalle,
                        m.fecha_expedida,
                        m.fecha_limite,
                        m.latitude,
                        m.longitude
                FROM dbo.Multas        m
                JOIN dbo.PlacasAutos   p  ON p.id_placa = m.id_placa
                JOIN dbo.TiposMulta    tm ON tm.id_tipo_multa = m.id_tipo_multa
                WHERE p.placa = @placa
                AND m.pagado = 0
                ORDER BY m.fecha_expedida DESC;
            ";

            var multas = await _db.QueryAsync(sqlMultas, new { placa });

            if (!multas.Any())
            {
                return NotFound($"No se encontraron multas pendientes para la placa {placa}");
            }

            // Generar PDF 
            var pdfBytes = await GenerarPdfMultas(multas.ToList(), placa);
            
            // Retornar PDF al navegador
            return File(pdfBytes, "application/pdf", $"Multas_{placa}_{DateTime.Now:yyyyMMdd}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar PDF para placa {Placa}", placa);
            return StatusCode(500, "Error interno del servidor");
        }
    }

    public record EnviarPdfDto(string Placa, string Email);

    //Enviar un correo con el pdf de las multas de una placa
    [HttpPost("enviar-pdf")]
    public async Task<IActionResult> EnviarPdfPorCorreo([FromBody] EnviarPdfDto dto)
    {
        _logger.LogInformation("eNVIAR PAPEL 1");
        try
        {
            // Consultar multas
            const string sqlMultas = @"
                SELECT  m.id_multa,
                        p.placa,
                        p.titular,
                        tm.titulo       AS tipo_multa,
                        m.monto,
                        m.direccion,
                        m.detalle,
                        m.fecha_expedida,
                        m.fecha_limite,
                        m.latitude,
                        m.longitude
                FROM dbo.Multas        m
                JOIN dbo.PlacasAutos   p  ON p.id_placa = m.id_placa
                JOIN dbo.TiposMulta    tm ON tm.id_tipo_multa = m.id_tipo_multa
                WHERE p.placa = @Placa
                AND m.pagado = 0
                ORDER BY m.fecha_expedida DESC;
            ";

            var multas = await _db.QueryAsync(sqlMultas, new { dto.Placa });

            if (!multas.Any())
            {
                return NotFound($"No se encontraron multas pendientes para la placa {dto.Placa}");
            }

            // Generar PDF
            var pdfBytes = await GenerarPdfMultas(multas.ToList(), dto.Placa);
            
            // Enviar por correo// Hard code el correo
            await EnviarCorreoConPdf("99diazluisfernand@gmail.com", dto.Placa, pdfBytes, multas.Sum(m => (decimal)m.monto));

            return Ok(new { 
                mensaje = "PDF enviado correctamente", 
                email = dto.Email,
                placa = dto.Placa,
                fecha_envio = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar PDF por correo para placa {Placa}", dto.Placa);
            return StatusCode(500, "Error al enviar correo");
        }
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
                    m.longitude,
                    m.pagado
            FROM dbo.Multas        m
            JOIN dbo.PlacasAutos   p  ON p.id_placa = m.id_placa
            JOIN dbo.TiposMulta    tm ON tm.id_tipo_multa = m.id_tipo_multa
            WHERE p.placa = @placa
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


    private async Task<byte[]> GenerarPdfMultas(List<dynamic> multas, string placa)
    {
        var doc = new MultasPdf { Placa = placa, Multas = multas };
        return doc.GeneratePdf();  
    }

    private DataTable CrearDataTableMultas(List<dynamic> multas, string placa)
    {
        var dataTable = new DataTable("MultasData");
        
        dataTable.Columns.Add("id_multa", typeof(int));
        dataTable.Columns.Add("placa", typeof(string));
        dataTable.Columns.Add("titular", typeof(string));
        dataTable.Columns.Add("tipo_multa", typeof(string));
        dataTable.Columns.Add("monto", typeof(decimal));
        dataTable.Columns.Add("direccion", typeof(string));
        dataTable.Columns.Add("detalle", typeof(string));
        dataTable.Columns.Add("fecha_expedida", typeof(DateTime));
        dataTable.Columns.Add("fecha_limite", typeof(DateTime));
        dataTable.Columns.Add("latitude", typeof(string));
        dataTable.Columns.Add("longitude", typeof(string));
        
        dataTable.Columns.Add("banco_referencia", typeof(string));
        dataTable.Columns.Add("banco_cuenta", typeof(string));
        dataTable.Columns.Add("banco_clabe", typeof(string));
        dataTable.Columns.Add("codigo_barras", typeof(string));

        foreach (var multa in multas)
        {
            var row = dataTable.NewRow();
            row["id_multa"] = multa.id_multa;
            row["placa"] = multa.placa;
            row["titular"] = multa.titular;
            row["tipo_multa"] = multa.tipo_multa;
            row["monto"] = multa.monto;
            row["direccion"] = multa.direccion;
            row["detalle"] = multa.detalle ?? "";
            row["fecha_expedida"] = multa.fecha_expedida;
            row["fecha_limite"] = multa.fecha_limite;
            row["latitude"] = multa.latitude;
            row["longitude"] = multa.longitude;
            
            var random = new Random();
            row["banco_referencia"] = $"REF{random.Next(100000, 999999)}";
            row["banco_cuenta"] = "4152-3138-7891-0001";
            row["banco_clabe"] = "014320415231387891";
            row["codigo_barras"] = GenerarCodigoBarras(multa.id_multa, (decimal)multa.monto);
            
            dataTable.Rows.Add(row);
        }

        return dataTable;
    }

    private string GenerarCodigoBarras(int idMulta, decimal monto)
    {
        var sb = new StringBuilder();
        sb.Append("01"); 
        sb.Append("014320"); 
        sb.Append(idMulta.ToString("D8")); 
        sb.Append(((int)(monto * 100)).ToString("D10")); 
        sb.Append(DateTime.Now.ToString("yyyyMMdd")); 
        
        return sb.ToString();
    }

    private async Task<byte[]> ReadStreamAsync(Stream stream)
    {
        using (var memoryStream = new MemoryStream())
        {
            await stream.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }
    }

    private async Task EnviarCorreoConPdf(string email, string placa, byte[] pdfBytes, decimal montoTotal)
    {
         var smtpConfig = _configuration.GetSection("SmtpConfig");
        var host       = smtpConfig["Host"];
        var port       = int.Parse(smtpConfig["Port"]!);
        var enableSsl  = bool.Parse(smtpConfig["EnableSsl"]!);
        var user       = smtpConfig["Username"];
        var pass       = smtpConfig["Password"];
        var fromEmail  = smtpConfig["FromEmail"];

        _logger.LogInformation("SMTP Config → {Host}:{Port}, SSL={SSL}, User={User}", host, port, enableSsl, user);

        using var smtpClient = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl   = enableSsl
        };

        var mailMessage = new MailMessage
        {
            From        = new MailAddress(fromEmail, "Movilidad y transito SPGG"),
            Subject     = $"Multas Transito SPGG - Placa {placa}",
            Body        = CrearCuerpoCorreo(placa, montoTotal),
            IsBodyHtml  = true
        };
        mailMessage.To.Add(email);

        mailMessage.Attachments.Add(new Attachment(new MemoryStream(pdfBytes),
                                                $"Multas_{placa}_{DateTime.Now:yyyyMMdd}.pdf",
                                                "application/pdf"));

        await smtpClient.SendMailAsync(mailMessage);
    }

    private string CrearCuerpoCorreo(string placa, decimal montoTotal)
    {
        return $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>multas de Transito SPGG - Placa {placa}</h2>
                <p>Estimado ciudadano</p>
                <p>Estado de cuenta de las multas pendientes para el automovil con placa <strong>{placa}</strong>.</p>
                <p><strong>Monto Total a Pagar: ${montoTotal:N2}</strong></p>
                <p>Para realizar el pago, puede utilizar cualquiera de los metodos mostrados en el documento adjunto.</p>
                <br>
                <p>Atte; <br>
                Transito SPGG</p>
                <hr>
                <p style='font-size: 12px; color: #666;'>
                Este es un mensaje automatico, por favor no responda a este correo.
                </p>
            </body>
            </html>";
    }
}
