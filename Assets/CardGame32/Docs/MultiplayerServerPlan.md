# 32 Cards Multiplayer Server Plan

## Current State

- Unity client has a local prototype scene and first-pass card rule evaluator.
- Android build tools are installed and configured.
- Multiplayer is not live yet. The current prototype does not connect different phones together.
- 3D art is still programmatic placeholder art, not final production models.

## Recommended First Online Version

For 4 to 8 players per room, use an authoritative server:

- The server owns shuffle, deal order, player turns, pot, scoring, knock/drive doubling, and settlement.
- The client only sends player actions such as fold, raise, knock, drive, ready, chat, and mute.
- Room state is pushed to every player in the same room.
- Players in different rooms never see each other's cards, chat, or game state.

## Database Timing

The real database should live with the cloud server, not on the phone.

During local development, mock accounts are enough. When the cloud server is ready, add a database and migrate the account/session data there. The phone may cache a login token locally, but the source of truth should be the server database.

## Database Tables

### users

- id
- phone_or_device_login_id
- display_name
- avatar_id
- chip_balance
- created_at
- last_login_at
- banned_until

### rooms

- id
- room_code
- host_user_id
- max_players
- status
- allow_text_chat
- allow_voice_chat
- created_at
- closed_at

### room_players

- room_id
- user_id
- seat_index
- connected
- ready
- score_at_join
- score_at_leave

### game_rounds

- id
- room_id
- round_index
- dealer_user_id
- deck_seed_hash
- pot_before
- pot_after
- winner_user_id
- created_at

### game_actions

- id
- room_id
- round_id
- user_id
- action_type
- amount
- action_order
- created_at

### chat_messages

- id
- room_id
- user_id
- message_type
- text
- created_at

## Room Code Design

- Room code: 6 digits, for example `482913`.
- Server generates the code and checks uniqueness among active rooms.
- Joining requires room code plus current room capacity check.
- Each room has 4 to 8 seats.
- Room state is stored by `room_id`; room code is only the short invite key.
- When the last player leaves or a room is idle too long, close the room.

## Text Chat

Use the same realtime connection as gameplay:

- Client sends `chat.send`.
- Server validates room membership.
- Server stores the message if persistence is enabled.
- Server broadcasts `chat.message` only to players in that room.

## Voice Chat

Do not build raw voice from scratch for the first version. Use a voice SDK:

- Preferred Unity route: Vivox for room voice channels.
- Alternative: Agora or Photon Voice.
- The game server creates or authorizes a voice channel per room.
- Client joins the voice channel after entering the room.
- Keep push-to-talk or mute-on-join enabled by default for mobile friendliness.

## Minimum Server Messages

Client to server:

- `auth.login`
- `room.create`
- `room.join`
- `room.leave`
- `room.ready`
- `game.action`
- `chat.send`
- `voice.mute`

Server to client:

- `room.snapshot`
- `room.player_joined`
- `room.player_left`
- `game.snapshot`
- `game.action_result`
- `game.round_result`
- `chat.message`
- `error`

## First Cloud Stack

Simple and enough for this game:

- Server: Node.js or C# ASP.NET Core with WebSocket.
- Database: PostgreSQL.
- Optional cache/session room state: Redis.
- Voice: Vivox or Agora.
- Deployment: one small cloud server is enough at the beginning.

For 7 to 8 players per room, the traffic is tiny. The important part is correctness and anti-cheat: the server must shuffle, deal, compare hands, and settle chips.
