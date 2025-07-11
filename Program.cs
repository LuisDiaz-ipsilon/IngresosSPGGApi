using System.Data;
using Microsoft.Data.SqlClient;
using Dapper; 

using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("SqlServer");

builder.Services.AddCors(o => o.AddPolicy("dev", p =>
    p.WithOrigins("http://localhost:5173")
     .AllowAnyHeader()
     .AllowAnyMethod()
));

builder.Services.AddScoped<IDbConnection>(_ => new SqlConnection(connectionString));
    
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

QuestPDF.Settings.License = LicenseType.Community;

app.UseHttpsRedirection();

app.UseCors("dev");

app.UseAuthorization();

app.MapControllers();

app.Run();
