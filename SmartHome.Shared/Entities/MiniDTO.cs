using SmartHome.Shared.Constantes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace SmartHome.Shared.Entities
{
    [Table("devices")]
    public class MiniDTO
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [MaxLength(25)]
        [Required]
        public string Nombre { get; set; } = "Mini";

        [Required]
        public EstadoEnum Estado { get; set; } = EstadoEnum.Off;

        virtual public ICollection<Usuario>? Usuarios { get; set; }
    }
}
