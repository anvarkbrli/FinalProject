using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.FpsEstimator;

public sealed class FpsEstimateRequestDTO
{
    [RegularExpression("^(az|en)$", ErrorMessage = "Dil yalnız az və ya en ola bilər.")]
    public string Language { get; set; } = "az";

    [Range(1, long.MaxValue, ErrorMessage = "Oyun ID-si müsbət olmalıdır.")]
    public long? GameId { get; set; }

    [Required(ErrorMessage = "Oyun adı boş ola bilməz.")]
    [StringLength(150, ErrorMessage = "Oyun adı maksimum 150 simvol ola bilər.")]
    public string GameName { get; set; } = string.Empty;

    [Range(1950, 2100, ErrorMessage = "Oyunun buraxılış ili düzgün deyil.")]
    public int? GameReleaseYear { get; set; }

    [Required(ErrorMessage = "GPU boş ola bilməz.")]
    [StringLength(150, ErrorMessage = "GPU adı maksimum 150 simvol ola bilər.")]
    public string Gpu { get; set; } = string.Empty;

    [Required(ErrorMessage = "CPU boş ola bilməz.")]
    [StringLength(150, ErrorMessage = "CPU adı maksimum 150 simvol ola bilər.")]
    public string Cpu { get; set; } = string.Empty;

    [Range(1, 512, ErrorMessage = "RAM 1-512 GB aralığında olmalıdır.")]
    public int RamGb { get; set; }

    [Range(1, 128, ErrorMessage = "VRAM 1-128 GB aralığında olmalıdır.")]
    public int? VramGb { get; set; }

    [Required(ErrorMessage = "Görüntü ölçüsü boş ola bilməz.")]
    [StringLength(20, ErrorMessage = "Görüntü ölçüsü maksimum 20 simvol ola bilər.")]
    public string Resolution { get; set; } = string.Empty;

    [Required(ErrorMessage = "Qrafika səviyyəsi boş ola bilməz.")]
    [StringLength(20, ErrorMessage = "Qrafika səviyyəsi maksimum 20 simvol ola bilər.")]
    public string GraphicsPreset { get; set; } = string.Empty;

    public bool RayTracingEnabled { get; set; }

    [StringLength(20, ErrorMessage = "Upscaler adı maksimum 20 simvol ola bilər.")]
    public string? Upscaler { get; set; }

    [StringLength(30, ErrorMessage = "Upscaler rejimi maksimum 30 simvol ola bilər.")]
    public string? UpscalerMode { get; set; }

    public bool FrameGenerationEnabled { get; set; }

    [StringLength(500, ErrorMessage = "Əlavə qeydlər maksimum 500 simvol ola bilər.")]
    public string? AdditionalNotes { get; set; }

    public bool RememberHardware { get; set; } = true;
}
