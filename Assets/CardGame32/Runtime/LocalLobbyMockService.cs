using System;
using System.Collections.Generic;

namespace CardGame32
{
    public class LocalLobbyMockService
    {
        private readonly Dictionary<string, RoomSnapshot> roomsByCode = new Dictionary<string, RoomSnapshot>();

        public RoomSnapshot CreateRoom(AccountProfile host, int maxPlayers, bool allowVoiceChat)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            string roomCode;
            do
            {
                roomCode = RoomCodeUtility.CreateRoomCode();
            }
            while (roomsByCode.ContainsKey(roomCode));

            RoomSnapshot room = new RoomSnapshot
            {
                RoomId = Guid.NewGuid().ToString("N"),
                Status = RoomStatus.Waiting,
                Settings = new RoomSettings
                {
                    RoomCode = roomCode,
                    MaxPlayers = Math.Max(4, Math.Min(8, maxPlayers)),
                    StartingScore = 50,
                    AllowTextChat = true,
                    AllowVoiceChat = allowVoiceChat,
                    CreatedAtUtc = DateTime.UtcNow.ToString("O")
                }
            };

            roomsByCode.Add(roomCode, room);
            JoinRoom(roomCode, host);
            AddSystemMessage(room, host.DisplayName + " \u521b\u5efa\u4e86\u623f\u95f4");
            return room;
        }

        public RoomSnapshot JoinRoom(string roomCode, AccountProfile account)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            if (account == null)
            {
                throw new ArgumentNullException(nameof(account));
            }

            RoomSnapshot room;
            if (!roomsByCode.TryGetValue(roomCode, out room))
            {
                throw new InvalidOperationException("\u623f\u95f4\u4e0d\u5b58\u5728");
            }

            if (room.IsFull)
            {
                throw new InvalidOperationException("\u623f\u95f4\u5df2\u6ee1");
            }

            for (int i = 0; i < room.Players.Count; i++)
            {
                if (room.Players[i].AccountId == account.AccountId)
                {
                    room.Players[i].Connected = true;
                    return room;
                }
            }

            room.Players.Add(new RoomPlayer
            {
                AccountId = account.AccountId,
                DisplayName = account.DisplayName,
                SeatIndex = room.Players.Count,
                Score = room.Settings.StartingScore,
                Ready = false,
                Connected = true,
                MicrophoneMuted = true
            });

            AddSystemMessage(room, account.DisplayName + " \u52a0\u5165\u4e86\u623f\u95f4");
            return room;
        }

        public bool TryGetRoom(string roomCode, out RoomSnapshot room)
        {
            return roomsByCode.TryGetValue(roomCode, out room);
        }

        public ChatMessage SendTextMessage(string roomCode, AccountProfile sender, string text)
        {
            if (sender == null)
            {
                throw new ArgumentNullException(nameof(sender));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Message text is required.", nameof(text));
            }

            RoomSnapshot room;
            if (!roomsByCode.TryGetValue(roomCode, out room))
            {
                throw new InvalidOperationException("\u623f\u95f4\u4e0d\u5b58\u5728");
            }

            ChatMessage message = new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                RoomCode = roomCode,
                SenderAccountId = sender.AccountId,
                SenderName = sender.DisplayName,
                Type = ChatMessageType.Text,
                Text = text.Trim(),
                SentAtUtc = DateTime.UtcNow.ToString("O")
            };

            room.RecentMessages.Add(message);
            TrimRecentMessages(room);
            return message;
        }

        public void SetMicrophoneMuted(string roomCode, string accountId, bool muted)
        {
            RoomSnapshot room;
            if (!roomsByCode.TryGetValue(roomCode, out room))
            {
                throw new InvalidOperationException("\u623f\u95f4\u4e0d\u5b58\u5728");
            }

            for (int i = 0; i < room.Players.Count; i++)
            {
                if (room.Players[i].AccountId == accountId)
                {
                    room.Players[i].MicrophoneMuted = muted;
                    return;
                }
            }
        }

        private static void AddSystemMessage(RoomSnapshot room, string text)
        {
            room.RecentMessages.Add(new ChatMessage
            {
                MessageId = Guid.NewGuid().ToString("N"),
                RoomCode = room.Settings.RoomCode,
                SenderName = "\u7cfb\u7edf",
                Type = ChatMessageType.System,
                Text = text,
                SentAtUtc = DateTime.UtcNow.ToString("O")
            });

            TrimRecentMessages(room);
        }

        private static void TrimRecentMessages(RoomSnapshot room)
        {
            while (room.RecentMessages.Count > 50)
            {
                room.RecentMessages.RemoveAt(0);
            }
        }
    }
}
