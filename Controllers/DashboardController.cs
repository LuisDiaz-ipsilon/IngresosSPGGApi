using System.Data;
using Dapper;
using Microsoft.AspNetCore.Mvc;


[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly IDbConnection _db;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDbConnection db, ILogger<DashboardController> logger)
    {
        _db     = db;
        _logger = logger;
    }

    [HttpGet("ingresos-ultimos-6-meses")]
    public async Task<IActionResult> GetIngresosUltimos6Meses()
    {
        const string sql = @"
            SELECT mes_inicio, expedido_total, pagado_total
            FROM dbo.v_ingresos_ultimos_seis_meses;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    // GET api/dashboard/indice-morosidad-mes
    [HttpGet("indice-morosidad-mes")]
    public async Task<IActionResult> GetIndiceMorosidadMes()
    {
        const string sql = @"
            SELECT mes_inicio, total_expedido, total_moroso, indice_morosidad
            FROM dbo.v_indice_morosidad_mes;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    [HttpGet("indice-morosidad-global")]
    public async Task<IActionResult> GetIndiceMorosidadGlobal()
    {
        const string sql = @"
            SELECT indice_morosidad_prediales, indice_morosidad_multas
            FROM dbo.v_indice_morosidad_global;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    // GET api/dashboard/prediales-morosidad-zonas-5km
    [HttpGet("prediales-morosidad-zonas-5km")]
    public async Task<IActionResult> GetPredialesMorosidadZonas5Km()
    {
        const string sql = @"
            SELECT 
                mes_inicio, grid_x, grid_y,
                center_latitude, center_longitude,
                total_prediales, total_expedido, total_moroso, indice_morosidad
            FROM dbo.v_prediales_morosidad_zonas_cinco_km;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    [HttpGet("saldo-por-pagar-prediales")]
    public async Task<IActionResult> SaldoPredialesAnio()
    {
        const string sql = @"
            SELECT 
                saldo_por_pagar
            FROM dbo.v_saldo_prediales_12m;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    [HttpGet("saldo-por-pagar-multas")]
    public async Task<IActionResult> SaldoMultasAnio()
    {
        const string sql = @"
            SELECT 
                saldo_por_pagar
            FROM dbo.v_saldo_multas_12m;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }

    [HttpGet("multas-velocidad-zonas-5km")]
    public async Task<IActionResult> GetZonaMultasVelocidad()
    {
        const string sql = @"
            SELECT 
                grid_x, grid_y, center_latitude,
                center_longitude, total_exceso_velocidad
            FROM dbo.v_multas_exceso_velocidad_zonas_5km;
        ";

        var result = await _db.QueryAsync(sql);
        return Ok(result);
    }


}
