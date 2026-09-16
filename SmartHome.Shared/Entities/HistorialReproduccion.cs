namespace SmartHome.Shared.Entities;

public sealed class HistorialReproduccion
{
    public int Id { get; set; }

    public string QueryTexto { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    public bool Exitoso { get; set; }
}
