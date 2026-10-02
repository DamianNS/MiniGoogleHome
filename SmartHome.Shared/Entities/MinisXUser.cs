using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace SmartHome.Shared.Entities
{
    [Table("minisxusers")]
    [PrimaryKey(nameof(MiniId), nameof(UserId))]
    public sealed class MinisXUser
    {
        [ForeignKey(nameof(MiniId))]
        public MiniDTO Mini { get; set; } = null!;
        public int MiniId { get; set; }

        [ForeignKey(nameof(UserId))]
        public Usuario Usuario { get; set; } = null!;
        public int UserId { get; set; }
    }
}
