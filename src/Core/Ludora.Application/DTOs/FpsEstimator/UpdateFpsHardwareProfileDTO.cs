using System.ComponentModel.DataAnnotations;

namespace Ludora.Application.DTOs.FpsEstimator
{
    public sealed class UpdateFpsHardwareProfileDTO
    {
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
    }
}
