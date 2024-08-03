using System;

namespace HumbleFrameServer.Lib
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AssemblyBuildDateAttribute(string buildDate) : Attribute
    {
        public string BuildDate { get; set; } = buildDate;
    }
}
