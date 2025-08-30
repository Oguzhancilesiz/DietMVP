using DietMVP.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DietMVP
{
    public static class AppSession
    {
        public static Profile? CurrentProfile { get; set; }
    }
}
