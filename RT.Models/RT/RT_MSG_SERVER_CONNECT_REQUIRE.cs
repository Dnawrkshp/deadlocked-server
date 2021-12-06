using RT.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models
{
    [ScertMessage(RT_MSG_TYPE.RT_MSG_SERVER_CONNECT_REQUIRE)]
    public class RT_MSG_SERVER_CONNECT_REQUIRE : BaseScertMessage
    {

        public override RT_MSG_TYPE Id => RT_MSG_TYPE.RT_MSG_SERVER_CONNECT_REQUIRE;

        // PS2 
        public byte[] PS2Contents = new byte[] { 0x02, 0x48, 0x02 };

        // PS3
        public byte ReqServerPassword = 0x00;
        public byte[] PS3Contents = new byte[] { 0x48, 0x02 };

        public override void Deserialize(Server.Common.Stream.MessageReader reader)
        {
            if (reader.MediusVersion == 112 || reader.MediusVersion == 113)
            {
                PS3Contents = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            } else {

                PS2Contents = reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position));
            }
        }

        protected override void Serialize(Server.Common.Stream.MessageWriter writer)
        {
            if(writer.MediusVersion == 112 || writer.MediusVersion == 113)
            {

                writer.Write(ReqServerPassword);
                writer.Write(PS3Contents);
            } else {
                writer.Write(PS2Contents);
            }
        }
        /* // Needs MediusVersion Check here.
        public override string ToString()
        {
			if(MediusVersion == 112 || writer.MediusVersion == 113)
            {
                return base.ToString() + " " +
                $"ServerPassword: {ReqServerPassword} " +
                $"PS2Contents: {BitConverter.ToString(PS2Contents)} " +
                $"PS3Contents: {BitConverter.ToString(PS3Contents)} ";
            } else {
                return base.ToString() + " " +
                $"PS2Contents: {BitConverter.ToString(PS2Contents)}";
            }
            
        }
        */
    }
}
