using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHome.Shared.Configuration
{
    public class JwtOptions
    {
        public string SecretKey { get; set; } = default!;
    }
}
