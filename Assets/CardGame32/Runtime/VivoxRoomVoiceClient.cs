using System;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Vivox;
using UnityEngine;

namespace CardGame32
{
    public class VivoxRoomVoiceClient : MonoBehaviour
    {
        public string roomChannelPrefix = "room_";
        public bool joinMuted = true;

        public bool IsReady { get; private set; }
        public bool IsInChannel { get; private set; }
        public string CurrentChannel { get; private set; }
        public string LastError { get; private set; }

        public async Task InitializeAndLoginAsync(string displayName)
        {
            LastError = string.Empty;

            try
            {
                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }

                if (VivoxService.Instance.InitializationState == VivoxInitializationState.Uninitialized)
                {
                    await VivoxService.Instance.InitializeAsync();
                }

                if (!VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LoginAsync(new LoginOptions
                    {
                        DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Player" : displayName
                    });
                }

                IsReady = true;
                SetMicrophoneMuted(joinMuted);
            }
            catch (Exception exception)
            {
                IsReady = false;
                LastError = exception.Message;
                Debug.LogWarning("Vivox initialization failed: " + exception);
                throw;
            }
        }

        public async Task JoinRoomVoiceAsync(string roomCode, string displayName)
        {
            if (string.IsNullOrWhiteSpace(roomCode))
            {
                throw new ArgumentException("Room code is required.", nameof(roomCode));
            }

            if (!IsReady)
            {
                await InitializeAndLoginAsync(displayName);
            }

            string channelName = roomChannelPrefix + SanitizeChannelSegment(roomCode);
            if (IsInChannel && CurrentChannel == channelName)
            {
                return;
            }

            if (IsInChannel)
            {
                await VivoxService.Instance.LeaveAllChannelsAsync();
            }

            await VivoxService.Instance.JoinGroupChannelAsync(channelName, ChatCapability.TextAndAudio);
            CurrentChannel = channelName;
            IsInChannel = true;
        }

        public void SetMicrophoneMuted(bool muted)
        {
            if (muted)
            {
                VivoxService.Instance.MuteInputDevice();
            }
            else
            {
                VivoxService.Instance.UnmuteInputDevice();
            }
        }

        public async Task LeaveRoomVoiceAsync()
        {
            if (!IsReady)
            {
                return;
            }

            await VivoxService.Instance.LeaveAllChannelsAsync();
            IsInChannel = false;
            CurrentChannel = string.Empty;
        }

        public async Task LogoutAsync()
        {
            if (!IsReady)
            {
                return;
            }

            await LeaveRoomVoiceAsync();
            await VivoxService.Instance.LogoutAsync();
            IsReady = false;
        }

        private static string SanitizeChannelSegment(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                builder.Append(char.IsLetterOrDigit(character) || character == '_' || character == '-' ? character : '_');
            }

            return builder.Length == 0 ? "default" : builder.ToString();
        }
    }
}
