﻿using RT.Common;
using Server.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models
{
    [ScertMessage(RT_MSG_TYPE.RT_MSG_CLIENT_CONNECT_TCP)]
    public class RT_MSG_CLIENT_CONNECT_TCP : BaseScertMessage
    {
        public override RT_MSG_TYPE Id => RT_MSG_TYPE.RT_MSG_CLIENT_CONNECT_TCP;

        // 
        public uint ARG1;
        public int AppId;
        public RSA_KEY Key;

        public string SessionKey = null;
        public string AccessToken = null;

        public override void Deserialize(BinaryReader reader)
        {
            SessionKey = null;
            AccessToken = null;

            ARG1 = reader.ReadUInt32();
            AppId = reader.ReadInt32();
            Key = reader.Read<RSA_KEY>();

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                SessionKey = reader.ReadString(Constants.SESSIONKEY_MAXLEN);
                AccessToken = reader.ReadString(Constants.NET_ACCESS_KEY_LEN);
            }
        }

        protected override void Serialize(BinaryWriter writer)
        {
            writer.Write(ARG1);
            writer.Write(AppId);
            writer.Write(Key ?? RSA_KEY.Empty);
        }

        public override string ToString()
        {
            return base.ToString() + " " +
                $"ARG1:{ARG1:X8} " +
                $"ARG2:{AppId:X8} " +
                $"Key:{Key}";
        }
    }
}
