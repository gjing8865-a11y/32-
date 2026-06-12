using System;
using System.Collections.Generic;

namespace CardGame32
{
    public enum RoomStatus
    {
        Waiting,
        Playing,
        Settling,
        Closed
    }

    public enum ChatMessageType
    {
        Text,
        System
    }

    [Serializable]
    public class AccountProfile
    {
        public string AccountId;
        public string DisplayName;
        public int ChipBalance = 50;
        public string CreatedAtUtc;
        public string LastLoginAtUtc;
    }

    [Serializable]
    public class RoomSettings
    {
        public string RoomCode;
        public int MinPlayers = 4;
        public int MaxPlayers = 8;
        public int StartingScore = 50;
        public bool AllowTextChat = true;
        public bool AllowVoiceChat = true;
        public string CreatedAtUtc;
    }

    [Serializable]
    public class RoomPlayer
    {
        public string AccountId;
        public string DisplayName;
        public int SeatIndex;
        public int Score;
        public bool Connected = true;
        public bool Ready;
        public bool MicrophoneMuted = true;
    }

    [Serializable]
    public class RoomSnapshot
    {
        public string RoomId;
        public RoomSettings Settings = new RoomSettings();
        public RoomStatus Status = RoomStatus.Waiting;
        public List<RoomPlayer> Players = new List<RoomPlayer>();
        public List<ChatMessage> RecentMessages = new List<ChatMessage>();

        public bool IsFull
        {
            get { return Players.Count >= Settings.MaxPlayers; }
        }
    }

    [Serializable]
    public class ChatMessage
    {
        public string MessageId;
        public string RoomCode;
        public string SenderAccountId;
        public string SenderName;
        public ChatMessageType Type;
        public string Text;
        public string SentAtUtc;
    }

    [Serializable]
    public class VoiceParticipant
    {
        public string AccountId;
        public string DisplayName;
        public bool JoinedVoice;
        public bool Muted = true;
        public bool Speaking;
    }

    public static class RoomCodeUtility
    {
        public static string CreateRoomCode()
        {
            uint hash = unchecked((uint)Guid.NewGuid().GetHashCode());
            int number = (int)(hash % 900000U) + 100000;
            return number.ToString();
        }
    }
}
