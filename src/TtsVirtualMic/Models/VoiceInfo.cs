namespace TtsVirtualMic.Models;

public sealed class VoiceInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    /// <summary>Clave del motor (piper, espeak, sapi).</summary>
    public string Engine { get; init; } = "";
    /// <summary>Parámetro propio del motor (ruta del modelo, voz eSpeak, nombre SAPI).</summary>
    public string Param { get; init; } = "";
    public int BasePitch { get; init; } = 50;
    public string Display => $"{Name}  ·  {Category}";
}
