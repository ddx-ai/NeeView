﻿#pragma warning disable CA1822

namespace NeeView
{
    public class EnvironmentAccessor
    {
        [WordNodeMember]
        public string NeeViewPath => Environment.AssemblyLocation;

        [WordNodeMember]
        public string UserSettingFilePath => SaveData.UserSettingFilePath;

        [WordNodeMember]
        public string PackageType => Environment.PackageType;

        [WordNodeMember]
        public string Version => Environment.DispVersionShort;

        [WordNodeMember]
        public string ProductVersion => Environment.ProductVersion + "." + Environment.BuildVersion;

        [WordNodeMember]
        public string DateVersion => Environment.DateVersion;

        [WordNodeMember]
        public string Revision => Environment.Revision;

        [WordNodeMember]
        public bool SelfContained => Environment.SelfContained;

        [WordNodeMember]
        public string OSVersion => Environment.OSVersion;

        [WordNodeMember]
        public string UserAgent => Environment.UserAgent;

        internal WordNode CreateWordNode(string name)
        {
            var node = WordNodeHelper.CreateClassWordNode(name, this.GetType());

            return node;
        }
    }
}
