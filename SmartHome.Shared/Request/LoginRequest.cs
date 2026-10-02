using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SmartHome.Shared.Request
{
    public class LoginRequest
    {
        
        public string Usuario { get; set; } = string.Empty;
        
        public string Password { get; set; } = string.Empty;
    }
}
