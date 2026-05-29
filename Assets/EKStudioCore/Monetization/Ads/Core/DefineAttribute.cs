using System;

namespace EKStudio.Monetization
{
    /// <summary>
    /// Define attribute for conditional compilation
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public class DefineAttribute : Attribute
    {
        public string Define { get; }
        public string TypeName { get; }
        public string[] FilePaths { get; }

        public DefineAttribute(string define, string typeName, string[] filePaths = null)
        {
            Define = define;
            TypeName = typeName;
            FilePaths = filePaths ?? new string[0];
        }
    }
}
