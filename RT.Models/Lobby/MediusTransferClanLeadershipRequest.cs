﻿using RT.Common;
using Server.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RT.Models.Lobby
{
    [MediusMessage(NetMessageTypes.MessageClassLobby, MediusLobbyMessageIds.TransferClanLeadership)]
    public class MediusTransferClanLeadershipRequest : BaseLobbyMessage, IMediusRequest
    {
        public override byte PacketType => (byte)MediusLobbyMessageIds.TransferClanLeadership;



        public MessageId MessageID { get; set; }
        public string SessionKey; // SESSIONKEY_MAXLEN
        public int NewLeaderAccountID;
        public string NewLeaderAccountName; // ACCOUNTNAME_MAXLEN

        public override void Deserialize(BinaryReader reader)
        {
            // 
            base.Deserialize(reader);

            // 
            MessageID = reader.Read<MessageId>();
            SessionKey = reader.ReadString(Constants.SESSIONKEY_MAXLEN);
            NewLeaderAccountID = reader.ReadInt32();
            NewLeaderAccountName = reader.ReadString(Constants.ACCOUNTNAME_MAXLEN);
        }

        public override void Serialize(BinaryWriter writer)
        {
            // 
            base.Serialize(writer);

            // 
            writer.Write(MessageID ?? MessageId.Empty);
            writer.Write(SessionKey, Constants.SESSIONKEY_MAXLEN);
            writer.Write(NewLeaderAccountID);
            writer.Write(NewLeaderAccountName, Constants.ACCOUNTNAME_MAXLEN);
        }


        public override string ToString()
        {
            return base.ToString() + " " +
                $"MessageID:{MessageID}" + " " +
                $"SessionKey:{SessionKey}" + " " +
                $"NewLeaderAccountID:{NewLeaderAccountID}" + " " +
                $"NewLeaderAccountName:{NewLeaderAccountName}";
        }
    }
}