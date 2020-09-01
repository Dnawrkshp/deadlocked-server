﻿using RT.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models
{
    [ScertMessage(RT_MSG_TYPE.RT_MSG_CLIENT_SET_AGG_TIME)]
    public class RT_MSG_CLIENT_SET_AGG_TIME : BaseScertMessage
    {
        public override RT_MSG_TYPE Id => RT_MSG_TYPE.RT_MSG_CLIENT_SET_AGG_TIME;

        public short AggTime { get; set; }

        public override void Deserialize(BinaryReader reader)
        {
            AggTime = reader.ReadInt16();
        }

        protected override void Serialize(BinaryWriter writer)
        {
            writer.Write(AggTime);
        }

        public override string ToString()
        {
            return base.ToString() + " " +
                $"AggTime:{AggTime}";
        }
    }
}
