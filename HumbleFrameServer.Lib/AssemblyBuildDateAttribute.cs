using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HumbleFrameServer.Lib
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AssemblyBuildDateAttribute : Attribute
    {
        public string BuildDate { get; set; }
    }
}
