using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class MultasPdf : IDocument
{
    // Inicializadores para evitar CS8618
    public string Placa { get; init; } = string.Empty;
    public IList<dynamic> Multas { get; init; } = Array.Empty<dynamic>();

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer doc)
    {
        doc.Page(page =>
        {
            page.Margin(20);
            page.PageColor(Colors.White);
            page.Size(PageSizes.A4);

            // Header
            page.Header()
                .Column(col =>
                {
                    col.Item()
                       .Text($"Pago Infracciones Tránsito SPGG — Placa {Placa}")
                       .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item()
                       .Text($"Generado: {DateTime.Now:dd-MM-yyyy HH:mm}");
                });

            // Content: multas, total, referencias bancarias
            page.Content()
                .PaddingVertical(10)
                .Column(col =>
                {
                    // 1) Tabla de multas
                    col.Item().Element(ComposeMultasTable);

                    // 2) Total a pagar
                    col.Item()
                       .AlignRight()
                       .Text($"Total a pagar: $ {Multas.Sum(x => (decimal)x.monto):N2}")
                       .FontSize(14).Bold();

                    // 3) Título de referencias
                    col.Item()
                       .PaddingTop(15)
                       .Text("Referencias para pago en bancos")
                       .FontSize(14).SemiBold();

                    // 4) Tabla de referencias demo
                    col.Item().Element(ComposeBancosTable);
                });

            // Footer con agradecimiento
            page.Footer()
                .AlignCenter()
                .Text("Cuida de los pequeños gastos; un pequeño agujero, hunde un barco.\nGracias por su pago. Atentamente, Luis Fernando Flores Diaz, Director de Economia. SPGG\n ")
                .FontSize(12).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    void ComposeMultasTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(35);
                c.RelativeColumn();
                c.ConstantColumn(80);
                c.ConstantColumn(80);
                c.ConstantColumn(80);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Multa");
                header.Cell().Element(HeaderCell).Text("Importe");
                header.Cell().Element(HeaderCell).Text("Fecha multa");
                header.Cell().Element(HeaderCell).Text("Límite");
            });

            int index = 1;
            foreach (var m in Multas)
            {
                table.Cell().Text(index++.ToString());
                table.Cell().Text((string)m.tipo_multa);
                table.Cell().Text($" $ {(decimal)m.monto:N2}");
                table.Cell().Text(((DateTime)m.fecha_expedida).ToString("dd/MM/yyyy"));
                table.Cell().Text(((DateTime)m.fecha_limite).ToString("dd/MM/yyyy"));
            }

            static IContainer HeaderCell(IContainer cell) => cell
                .PaddingVertical(4)
                .Background(Colors.Grey.Lighten3)
                .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
                .DefaultTextStyle(ts => ts.SemiBold());
        });
    }

    void ComposeBancosTable(IContainer container)
    {
        var referencias = new[]
        {
            new { Banco="BBVA Bancomer", Referencia="REF123456", Cuenta="4152-3138-7891-0001", Clabe="014320415231387891" },
            new { Banco="AFIRME",         Referencia="REF654321", Cuenta="4152-0000-1111-2222", Clabe="014320415200011122" },
            new { Banco="Banorte",        Referencia="REF998877", Cuenta="4152-3333-4444-5555", Clabe="014320415233334455" }
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);
                c.RelativeColumn(3);
                c.RelativeColumn(3);
                c.RelativeColumn(4);
            });

            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Banco");
                header.Cell().Element(HeaderCell).Text("Referencia");
                header.Cell().Element(HeaderCell).Text("Cuenta");
                header.Cell().Element(HeaderCell).Text("CLABE");
            });

            foreach (var r in referencias)
            {
                table.Cell().Text(r.Banco);
                table.Cell().Text(r.Referencia);
                table.Cell().Text(r.Cuenta);
                table.Cell().Text(r.Clabe);
            }

            static IContainer HeaderCell(IContainer cell) => cell
                .PaddingVertical(4)
                .Background(Colors.Grey.Lighten3)
                .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
                .DefaultTextStyle(ts => ts.SemiBold());
        });
    }
}
