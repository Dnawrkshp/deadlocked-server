using RT.Common;
using Server.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models
{
	[MediusMessage(NetMessageTypes.MessageClassLobbyExt, MediusLobbyExtMessageIds.UniverseVariableSvoURLResponse)]
    public class MediusUniverseVariableSvoURLResponse : BaseLobbyExtMessage, IMediusResponse
    {
		public override byte PacketType => (byte)MediusLobbyExtMessageIds.UniverseVariableSvoURLResponse;

        public bool IsSuccess => true;

        public MessageId MessageID { get; set; }

        public string URL { get; set; }

        public override void Deserialize(Server.Common.Stream.MessageReader reader)
        {
            //
            base.Deserialize(reader);

            //
            MessageID = reader.Read<MessageId>();

            // read URL
            if (reader.MediusVersion >= 109)
            {
                // 1 byte length prefixed url
                byte len = reader.ReadByte();
                URL = reader.ReadString(len);
            }
            else
            {
                // fixed size url
                URL = reader.ReadString(128);
            }
        }

        public override void Serialize(Server.Common.Stream.MessageWriter writer)
        {
            // 
            base.Serialize(writer);

            //
            writer.Write(MessageID ?? MessageId.Empty);

            // Write URL
            if (writer.MediusVersion >= 109)
            {
                // 1 byte length prefixed url
                writer.Write((byte)URL.Length);
                writer.Write(URL, URL.Length);
            }
            else
            {
                // fixed size url
                writer.Write(URL, 128);
            }
        }

    }
}
