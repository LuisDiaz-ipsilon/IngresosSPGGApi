using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

public class MultasPdf : IDocument
{
    public  string Placa { get; init; }
    public IList<dynamic> Multas { get; init; }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer doc)
    {
        doc.Page(page =>
        {
            page.Margin(20);
            page.PageColor(Colors.White);
            page.Size(PageSizes.A4);

            page.Header()
                .Column(col =>
                {
                    col.Item().Text($"Pago Infracciones Transito SPGG - Placa {Placa}")
                              .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text($"Generado: {DateTime.Now:dd-MM-yyyy HH:mm}");
                });

            page.Content()
                .PaddingVertical(10)
                .Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(35); // #
                        c.RelativeColumn();   // titulo multa
                        c.ConstantColumn(100); // Importe
                        c.ConstantColumn(100); // Expedida
                        c.ConstantColumn(70); // Límite
                    });

                    // Encabezado
                    table.Header(h =>
                    {
                        h.Cell().Element(HeaderCell).Text("#");
                        h.Cell().Element(HeaderCell).Text("Multa");
                        h.Cell().Element(HeaderCell).Text("Importe");
                        h.Cell().Element(HeaderCell).Text("Fecha de la multa");
                        h.Cell().Element(HeaderCell).Text("Límite");

                        static IContainer HeaderCell(IContainer c) => c
                            .PaddingVertical(4)
                            .Background(Colors.Grey.Lighten3)
                            .BorderBottom(1)
                            .BorderColor(Colors.Grey.Darken2)
                            .DefaultTextStyle(ts => ts.SemiBold());
                    });

                    // Filas
                    int i = 1;
                    foreach (var m in Multas)
                    {
                        table.Cell().Text(i++.ToString());
                        table.Cell().Text((string)m.tipo_multa);
                        table.Cell().Text($"{(decimal)m.monto:N2}");
                        table.Cell().Text(((DateTime)m.fecha_expedida).ToString("dd/MM/yy"));
                        table.Cell().Text(((DateTime)m.fecha_limite).ToString("dd/MM/yy"));
                    }
                });

            page.Footer()
                .AlignRight()
                .Text($"Total a pagar: {Multas.Sum(x => (decimal)x.monto):N2}")
                .FontSize(14).Bold();
        });
    }

    // Método privado para renderizar la tabla de multas
    void ComposeMultasTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(35);   // #
                c.RelativeColumn();     // Concepto
                c.ConstantColumn(80);   // Importe
                c.ConstantColumn(80);   // Expedida
                c.ConstantColumn(80);   // Límite
            });

            // Encabezados
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("#");
                header.Cell().Element(HeaderCell).Text("Multa");
                header.Cell().Element(HeaderCell).AlignRight().Text("Importe");
                header.Cell().Element(HeaderCell).Text("Fecha multa");
                header.Cell().Element(HeaderCell).Text("Límite");
            });

            // Filas de datos
            int index = 1;
            foreach (var m in Multas)
            {
                table.Cell().Text(index++.ToString());
                table.Cell().Text((string)m.tipo_multa);
                table.Cell().AlignRight().Text($"{(decimal)m.monto:N2}");
                table.Cell().Text(((DateTime)m.fecha_expedida).ToString("dd/MM/yyyy"));
                table.Cell().Text(((DateTime)m.fecha_limite).ToString("dd/MM/yyyy"));
            }

            static IContainer HeaderCell(IContainer cell) => cell
                .PaddingVertical(4)
                .Background(Colors.Grey.Lighten3)
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Darken2)
                .DefaultTextStyle(ts => ts.SemiBold());
        });
    }

    // Método privado para renderizar la tabla de referencias bancarias (datos demo)
    void ComposeBancosTable(IContainer container)
    {
        // Ejemplo de datos hardcodeados
        var referencias = new[]
        {
            new { Banco = "BBVA Bancomer", Referencia = "REF123456", Cuenta = "4152-3138-7891-0001", Clabe = "014320415231387891" },
            new { Banco = "AFIRME", Referencia = "REF654321", Cuenta = "4152-0000-1111-2222", Clabe = "014320415200011122" },
            new { Banco = "Banorte", Referencia = "REF998877", Cuenta = "4152-3333-4444-5555", Clabe = "014320415233334455" }
        };

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(2);    // Banco
                c.RelativeColumn(3);    // Referencia
                c.RelativeColumn(3);    // Cuenta
                c.RelativeColumn(4);    // Clabe
            });

            // Encabezados
            table.Header(header =>
            {
                header.Cell().Element(HeaderCell).Text("Banco");
                header.Cell().Element(HeaderCell).Text("Referencia");
                header.Cell().Element(HeaderCell).Text("Cuenta");
                header.Cell().Element(HeaderCell).Text("CLABE");
            });

            // Filas demo
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
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Darken2)
                .DefaultTextStyle(ts => ts.SemiBold());
        });
    }

}
