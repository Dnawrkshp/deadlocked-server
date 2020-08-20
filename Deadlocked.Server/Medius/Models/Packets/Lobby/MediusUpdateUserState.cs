using Deadlocked.Server.Stream;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Deadlocked.Server.Medius.Models.Packets.Lobby
{
	[MediusMessage(NetMessageTypes.MessageClassLobby, MediusLobbyMessageIds.UpdateUserState)]
    public class MediusUpdateUserState : BaseMediusMessage
    {

		public override NetMessageTypes PacketClass => NetMessageTypes.MessageClassLobby;
		public override byte PacketType => (byte)MediusLobbyMessageIds.UpdateUserState;

        public string SessionKey; // SESSIONKEY_MAXLEN
        public MediusUserAction UserAction;

        public override void Deserialize(BinaryReader reader)
        {
            // 
            base.Deserialize(reader);

            // 
            SessionKey = reader.ReadString(MediusConstants.SESSIONKEY_MAXLEN);
            reader.ReadBytes(3);
            UserAction = reader.Read<MediusUserAction>();
        }

        public override void Serialize(BinaryWriter writer)
        {
            // 
            base.Serialize(writer);

            // 
            writer.Write(SessionKey, MediusConstants.SESSIONKEY_MAXLEN);
            writer.Write(new byte[3]);
            writer.Write(UserAction);
        }


        public override string ToString()
        {
            return base.ToString() + " " +
             $"SessionKey:{SessionKey}" + " " +
$"UserAction:{UserAction}";
        }
    }
}