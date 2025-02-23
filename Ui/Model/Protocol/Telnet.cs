﻿using System;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils.KiTTY;
using Shawn.Utils;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.Model;

namespace _1RM.Model.Protocol
{
    public class Telnet : ProtocolBaseWithAddressPort, IKittyConnectable
    {
        public static string ProtocolName = "Telnet";
        public Telnet() : base(Telnet.ProtocolName, "Putty.Telnet.V1", "Telnet")
        {
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<Telnet>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 3;
        }

        public string GetPuttyConnString(DataSourceBase _)
        {
            return $@" -load ""{this.GetSessionName()}"" -telnet {Address} -P {Port}";
        }

        private string _startupAutoCommand = "";

        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        [JsonIgnore]
        public ProtocolBase ProtocolBase => this;

        private string _externalKittySessionConfigPath = "";
        public string ExternalKittySessionConfigPath
        {
            get => _externalKittySessionConfigPath;
            set => SetAndNotifyIfChanged(ref _externalKittySessionConfigPath, value);
        }

        public string GetExeFullPath()
        {
            return this.GetKittyExeFullName();
        }

        public string GetExeArguments(DataSourceBase sourceService)
        {
            return GetPuttyConnString(sourceService);
        }
    }
}