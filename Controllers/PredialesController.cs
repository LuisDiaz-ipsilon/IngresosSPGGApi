using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers; 


[ApiController]
[Route("api/[controller]")]
public class PredialesController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly ILogger<PredialesController> _logger;
    private readonly IConfiguration _configuration;

    public PredialesController(
        IDbConnection db,
        ILogger<PredialesController> logger,
        IConfiguration configuration)
    {
        _db = db;
        _logger = logger;
        _configuration = configuration;
    }

    // PDF de los prediales pendientes para un domicilio
    [HttpGet("pdf/{domicilioId}")]
    public async Task<IActionResult> GetPdfByDomicilio(int domicilioId)
    {
        // 1.1 Consulta
        const string sql = @"
            SELECT id_predial, monto, fecha_expedida, fecha_limite
            FROM dbo.Prediales
            WHERE id_domicilio = @domicilioId
                AND pagado = 0
            ORDER BY fecha_expedida DESC;
        ";
        var prediales = (await _db.QueryAsync(sql, new { domicilioId })).ToList();
        if (!prediales.Any())
            return NotFound($"No hay prediales pendientes para el domicilio {domicilioId}");

        // 1.2 Genera PDF
        var pdfBytes = await GenerarPdfPrediales(prediales, domicilioId);

        // 1.3 Devuelve el PDF
        return File(
            pdfBytes,
            "application/pdf",
            $"Prediales_{domicilioId}_{DateTime.Now:yyyyMMdd}.pdf"
        );
    }

    public record EnviarPdfPredialDto(int DomicilioId, string Email);

    // 2) Enviar PDF por correo
    [HttpPost("enviar-pdf")]
    public async Task<IActionResult> EnviarPdfPorCorreo([FromBody] EnviarPdfPredialDto dto)
    {
        // 2.1 Consulta
        const string sql = @"
            SELECT id_predial, monto, fecha_expedida, fecha_limite
            FROM dbo.Prediales
            WHERE id_domicilio = @DomicilioId
                AND pagado = 0
            ORDER BY fecha_expedida DESC;
        ";
        var prediales = (await _db.QueryAsync(sql, new { dto.DomicilioId })).ToList();
        
        if (!prediales.Any())
            return NotFound($"No hay prediales pendientes para el domicilio {dto.DomicilioId}");

        // Genera PDF
        var pdfBytes = await GenerarPdfPrediales(prediales, dto.DomicilioId);

        var montoTotal = prediales.Sum(x => (decimal)x.monto);
        await EnviarCorreoConPdf("99diazluisfernand@gmail.com", dto.DomicilioId, pdfBytes, montoTotal);

        return Ok(new {
            mensaje   = "PDF enviado correctamente",
            email     = dto.Email,
            domicilio = dto.DomicilioId,
            fecha_envio = DateTime.Now
        });
    }

    // 3) Listar prediales pendientes
    [HttpGet("{domicilioId}")]
    public async Task<IActionResult> GetByDomicilio(int domicilioId)
    {
        const string sql = @"
            SELECT id_predial,
                    FORMAT(monto, 'N2','en-US') AS monto,
                    FORMAT(fecha_expedida,'dd-MM-yyyy') AS fecha_expedida,
                    FORMAT(fecha_limite,  'dd-MM-yyyy') AS fecha_limite,
                    pagado
            FROM dbo.Prediales
            WHERE id_domicilio = @domicilioId
            ORDER BY fecha_expedida DESC;
        ";
        var list = await _db.QueryAsync(sql, new { domicilioId });
        return Ok(list);
    }

    public record PagarPredialDto(int IdPredial, decimal MontoPagar);

    // 4) Pagar predial individual
    [HttpPost("pagar")]
    public async Task<IActionResult> PagarPredial([FromBody] PagarPredialDto dto)
    {
        const string sel = @"
            SELECT id_predial, monto, fecha_limite, fecha_pago
            FROM dbo.Prediales
            WHERE id_predial = @IdPredial
                AND pagado = 0;
        ";
        var pred = await _db.QuerySingleOrDefaultAsync(sel, new { dto.IdPredial });
        if (pred is null)
            return NotFound($"Predial {dto.IdPredial} no existe o ya pagado.");

        if ((decimal)pred.monto != dto.MontoPagar)
            return BadRequest($"Monto incorrecto. Original: {pred.monto}, enviado: {dto.MontoPagar}");

        bool aTiempo = DateTime.UtcNow.Date <= ((DateTime)pred.fecha_limite).Date;
        const string upd = @"
            UPDATE dbo.Prediales
            SET fecha_pago    = @Now,
                pago_a_tiempo = @aTiempo,
                pagado        = 1
            WHERE id_predial = @IdPredial;
        ";
        await _db.ExecuteAsync(upd, new {
            Now = DateTime.UtcNow,
            aTiempo,
            dto.IdPredial
        });

        return Ok(new {
            mensaje       = "Predial pagado correctamente",
            id_predial    = dto.IdPredial,
            fecha_pago    = DateTime.UtcNow,
            pago_a_tiempo = aTiempo
        });
    }

    // 5) Total a deber por domicilio
    [HttpGet("total/{domicilioId}")]
    public async Task<IActionResult> GetTotalByDomicilio(int domicilioId)
    {
        const string sql = @"
            SELECT SUM(monto) FROM dbo.Prediales
            WHERE id_domicilio = @domicilioId
                AND pagado = 0;
        ";
        var total = (await _db.ExecuteScalarAsync<decimal?>(sql, new { domicilioId })) ?? 0m;
        return Ok(new { domicilioId, total_a_pagar = total });
    }

    public record PagarTotalPredialDto(int DomicilioId, decimal MontoPagar);

    // 6) Pagar todo
    [HttpPost("pagar-total")]
    public async Task<IActionResult> PagarTotalPredial([FromBody] PagarTotalPredialDto dto)
    {

        if (_db.State != System.Data.ConnectionState.Open)
        _db.Open();

        var sqlSum = @"
            SELECT SUM(monto) FROM dbo.Prediales
            WHERE id_domicilio = @DomicilioId
                AND pagado = 0;
        ";
        var pendiente = (await _db.ExecuteScalarAsync<decimal?>(sqlSum, dto)) ?? 0m;
        if (pendiente == 0) return BadRequest("No hay prediales pendientes.");
        if (pendiente != dto.MontoPagar)
            return BadRequest($"Monto incorrecto. Pendiente: {pendiente:0.00}");

        const string upd = @"
            UPDATE dbo.Prediales
            SET fecha_pago    = @Now,
                pago_a_tiempo = CASE WHEN @Now <= fecha_limite THEN 1 ELSE 0 END,
                pagado        = 1
            WHERE id_domicilio = @DomicilioId
                AND pagado = 0;
        ";
        
        using var tx = _db.BeginTransaction();
        var filas = await _db.ExecuteAsync(upd, new { dto.DomicilioId, Now = DateTime.UtcNow }, tx);
        tx.Commit();

        return Ok(new {
            domicilio      = dto.DomicilioId,
            total_pagado   = pendiente,
            registros_pago = filas,
            fecha_pago     = DateTime.UtcNow
        });
    }

    // --- El método GénérarPdf muy cercano al de multas ---
    private async Task<byte[]> GenerarPdfPrediales(List<dynamic> prediales, int domicilioId)
    {
        var doc = new PredialesPdf { DomicilioId = domicilioId, Prediales = prediales};
        return doc.GeneratePdf();
    }

    // Reusa tu helper de correo
    private async Task EnviarCorreoConPdf(string email, int domicilioId, byte[] pdfBytes, decimal montoTotal)
    {
        var smtp = _configuration.GetSection("SmtpConfig");
        var host = smtp["Host"]!;
        var port = int.Parse(smtp["Port"]!);
        var ssl  = bool.Parse(smtp["EnableSsl"]!);
        var user = smtp["Username"]!;
        var pass = smtp["Password"]!;
        var from = smtp["FromEmail"]!;

        _logger.LogInformation("SMTP Config → {Host}:{Port}, SSL={SSL}, User={User}", host, port, ssl, user);

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl   = ssl
        };

        var mailMessage = new MailMessage
        {
            From       = new MailAddress(from, "Predial SPGG"),
            Subject    = $"Predial SPGG - Domicilio {domicilioId}",
            Body       = CrearCuerpoCorreo(domicilioId, montoTotal),
            IsBodyHtml = true
        };
        mailMessage.To.Add(email);
        mailMessage.Attachments.Add(new Attachment(new MemoryStream(pdfBytes),
                                            $"Predial_{domicilioId}_{DateTime.Now:yyyyMMdd}.pdf",
                                            "application/pdf"));

        await client.SendMailAsync(mailMessage);
    }

    private string CrearCuerpoCorreo(int domicilio, decimal montoTotal)
    {
        return $@"
            <html>
            <body style='font-family: Arial, sans-serif;'>
                <h2>Vivienda SPGG Predial - Domicilio {domicilio}</h2>
                <p>Estimado ciudadano</p>
                <p>Estado de cuenta de Prediales pendientes <strong>{domicilio}</strong>.</p>
                <p><strong>Monto Total a Pagar: ${montoTotal:N2}</strong></p>
                <p>Para realizar el pago, puede utilizar cualquiera de los metodos mostrados en el documento adjunto.</p>
                <br>
                <p>Atte; <br>
                Vivienda SPGG</p>
                <hr>
                <p style='font-size: 12px; color: #666;'>
                Este es un mensaje automatico, por favor no responda a este correo.
                </p>
            </body>
            </html>";
    }
}

