using Miches360.Dominio.Catalogo;
using Miches360.Dominio.Enums;
using Miches360.Dominio.Mesas;

namespace Miches360.Infraestructura.Persistencia;

internal static class DatosIniciales
{
    public static void Cargar(AppDbContext db)
    {
        var micheladas = new Categoria("Micheladas", "#C66A43", EstacionPreparacion.Barra, manejaTamanos: true, orden: 1);
        var cheladas = new Categoria("Cheladas", "#F2D9AE", EstacionPreparacion.Barra, manejaTamanos: true, orden: 2);
        var preparados = new Categoria("Preparados", "#3E8EDE", EstacionPreparacion.Barra, manejaTamanos: true, orden: 3);
        var cervezas = new Categoria("Cervezas", "#E8B13A", EstacionPreparacion.YaListo, manejaTamanos: false, orden: 4);
        var snacks = new Categoria("Snacks", "#E07B39", EstacionPreparacion.Cocina, manejaTamanos: false, orden: 5);
        var dulceria = new Categoria("Dulcería", "#D9485F", EstacionPreparacion.YaListo, manejaTamanos: false, orden: 6);
        var extras = new Categoria("Extras", "#8FB9A8", EstacionPreparacion.Barra, manejaTamanos: false, orden: 7);

        db.Categorias.AddRange(micheladas, cheladas, preparados, cervezas, snacks, dulceria, extras);

        var orden = 0;

        Producto ConTamanos(Categoria categoria, string nombre, string color, decimal? chico, decimal? mediano, decimal? litro)
        {
            var producto = new Producto(categoria.Id, nombre, color, ++orden);
            if (chico is decimal c) producto.FijarPrecio(Tamano.Chico, c);
            if (mediano is decimal m) producto.FijarPrecio(Tamano.Mediano, m);
            if (litro is decimal l) producto.FijarPrecio(Tamano.Litro, l);
            db.Productos.Add(producto);
            return producto;
        }

        Producto Unico(Categoria categoria, string nombre, string color, decimal precio)
        {
            var producto = new Producto(categoria.Id, nombre, color, ++orden);
            producto.FijarPrecio(Tamano.Unico, precio);
            db.Productos.Add(producto);
            return producto;
        }

        ConTamanos(micheladas, "Michelada clásica", "#C66A43", 70, 85, 120);
        ConTamanos(micheladas, "Michelada cubana", "#8F311A", 75, 90, 125);
        ConTamanos(micheladas, "Miche de mango", "#E8B13A", 90, 110, 150);
        ConTamanos(micheladas, "Miche de tamarindo", "#7A4A2A", 90, 110, 150);
        ConTamanos(micheladas, "Gomichela", "#D9485F", null, 120, 160);
        ConTamanos(micheladas, "Miche con camarón", "#B5452A", null, 140, 185);

        ConTamanos(cheladas, "Chelada", "#F2D9AE", null, 75, 105);
        ConTamanos(cheladas, "Clamato preparado", "#9C6B3A", null, 95, 130);

        ConTamanos(preparados, "Azulito", "#3E8EDE", null, 100, 140);
        ConTamanos(preparados, "Cantarito", "#E07B39", null, 110, null);
        ConTamanos(preparados, "Mineral preparada", "#8FB9A8", 60, 75, null);

        Unico(cervezas, "Cerveza clara", "#E8B13A", 45);
        Unico(cervezas, "Cerveza oscura", "#7A4A2A", 45);

        Unico(snacks, "Papas preparadas", "#E07B39", 75);
        Unico(snacks, "Tostitos preparados", "#E8B13A", 85);
        Unico(snacks, "Elote en vaso", "#E8D27A", 60);
        Unico(snacks, "Chicharrón preparado", "#C66A43", 70);
        Unico(snacks, "Pepinos con chamoy", "#8FB9A8", 55);

        Unico(dulceria, "Pulparindo", "#7A4A2A", 15);
        Unico(dulceria, "Gomitas enchiladas", "#D9485F", 25);
        Unico(dulceria, "Cacahuates japoneses", "#E8B13A", 35);
        Unico(dulceria, "Paleta enchilada", "#B5452A", 15);

        Unico(extras, "Escarchado de chamoy", "#B5452A", 15);
        Unico(extras, "Gomitas extra", "#D9485F", 15);
        Unico(extras, "Tajín extra", "#C66A43", 10);
        Unico(extras, "Camarón extra", "#E07B39", 30);

        for (var i = 1; i <= 10; i++)
        {
            db.Mesas.Add(new Mesa($"Mesa {i}", i));
        }

        db.Mesas.Add(new Mesa("Barra 1", 11));
        db.Mesas.Add(new Mesa("Barra 2", 12));
    }
}
