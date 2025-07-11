using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class PredialesPdf : IDocument
{
    // Evita CS8618 / nulos
    public int DomicilioId { get; init; }
    public IList<dynamic> Prediales { get; init; } = Array.Empty<dynamic>();

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer doc)
    {
        doc.Page(page =>
        {
            page.Margin(20);
            page.PageColor(Colors.White);
            page.Size(PageSizes.A4);

            /* ---------- HEADER ---------- */
            page.Header()
                .Column(col =>
                {
                    col.Item()
                       .Text($"Pago Predial SPGG — Domicilio #{DomicilioId}")
                       .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);

                    col.Item()
                       .Text($"Generado: {DateTime.Now:dd-MM-yyyy HH:mm}");
                });

            /* ---------- CONTENT ---------- */
            page.Content()
                .PaddingVertical(10)
                .Column(col =>
                {
                    /* 1) Tabla de prediales pendientes */
                    col.Item().Element(ComposePredialesTable);

                    /* 2) Total a pagar */
                    col.Item()
                       .AlignRight()
                       .Text($"Total a pagar: $ {Prediales.Sum(x => (decimal)x.monto):N2}")
                       .FontSize(14).Bold();

                    /* 3) Referencias bancarias de demostración */
                    col.Item()
                       .PaddingTop(15)
                       .Text("Referencias para pago en bancos")
                       .FontSize(14).SemiBold();

                    col.Item().Element(ComposeBancosTable);
                });

            /* ---------- FOOTER ---------- */
            page.Footer()
                .AlignCenter()
                .Text("Cuida de los pequeños gastos; un pequeño agujero hunde un barco.\n" +
                      "Gracias por su pago. Atentamente, Tesorería SPGG.")
                .FontSize(12).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    /* ======= Tabla principal ======= */
    void ComposePredialesTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(35);  // #
                c.ConstantColumn(100); // Importe
                c.ConstantColumn(100); // Expedida
                c.ConstantColumn(100); // Límite
            });

            /* Encabezado */
            table.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("#");
                h.Cell().Element(HeaderCell).Text("Importe");
                h.Cell().Element(HeaderCell).Text("Expedido");
                h.Cell().Element(HeaderCell).Text("Fecha limite");

                static IContainer HeaderCell(IContainer c) => c
                    .PaddingVertical(4)
                    .Background(Colors.Grey.Lighten3)
                    .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
                    .DefaultTextStyle(ts => ts.SemiBold());
            });

            /* Filas */
            int i = 1;
            foreach (var p in Prediales)
            {
                table.Cell().Text(i++.ToString());
                table.Cell().Text($"$ {(decimal)p.monto:N2}");
                table.Cell().Text(((DateTime)p.fecha_expedida).ToString("dd/MM/yyyy"));
                table.Cell().Text(((DateTime)p.fecha_limite  ).ToString("dd/MM/yyyy"));
            }
        });
    }

    /* ======= Tabla demo de bancos ======= */
    void ComposeBancosTable(IContainer container)
    {
        var refs = new[]
        {
            new { Banco="BBVA Bancomer", Referencia="PDREF123", Cuenta="4152-3138-7891-0001", Clabe="014320415231387891" },
            new { Banco="AFIRME",         Referencia="PDREF456", Cuenta="4152-0000-1111-2222", Clabe="014320415200011122" },
            new { Banco="Banorte",        Referencia="PDREF789", Cuenta="4152-3333-4444-5555", Clabe="014320415233334455" }
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

            table.Header(h =>
            {
                h.Cell().Element(HeaderCell).Text("Banco");
                h.Cell().Element(HeaderCell).Text("Referencia");
                h.Cell().Element(HeaderCell).Text("Cuenta");
                h.Cell().Element(HeaderCell).Text("CLABE");

                static IContainer HeaderCell(IContainer c) => c
                    .PaddingVertical(4)
                    .Background(Colors.Grey.Lighten3)
                    .BorderBottom(1).BorderColor(Colors.Grey.Darken2)
                    .DefaultTextStyle(ts => ts.SemiBold());
            });

            foreach (var r in refs)
            {
                table.Cell().Text(r.Banco);
                table.Cell().Text(r.Referencia);
                table.Cell().Text(r.Cuenta);
                table.Cell().Text(r.Clabe);
            }
        });
    }
}
