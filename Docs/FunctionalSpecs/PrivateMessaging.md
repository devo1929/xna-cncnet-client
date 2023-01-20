# Private Messaging Functional Specs

This should outline how private messaging works, both UI and non-UI.

## Terms

- PMW - Private Messaging Window

## Top Bar - `Private Messages` button

- Click while PMW closed - Open the PMW
- Click while PMW open - do nothing

## PMW - Private Messaging Window

- Has a darkening panel background
  - Clicking background will close PMW
- Has a text input field for creating messages

### Tabs - Contains 4 tabs across the top of PMW
#### Messages tab

- User list - current conversations since application start
  - Sorted by:
    - Online descending (online first)
    - Username ascending
  - Online users appear as enabled
    - If user logs out, they will convert to offline visibility immediately
  - Offline users appear as disabled
    - If user logs in, they will convert to online visibility immediately
  - Selecting an online user will enable the text input field
  - Selecting an offline user will disable the text input field
- Message list - list of message for currently selected user

#### Friend List tab

- User list - current list of friends
  - Sorted by:
    - Online descending (online first)
    - Username ascending
  - Online users appear as enabled
    - If user logs out, they will convert to offline visibility immediately
  - Offline users appear as disabled
    - If user logs in, they will convert to online visibility immediately
  - Selecting an online user will enable the text input field
  - Selecting an offline user will disable the text input field
- Message list - list of message for currently selected user
- 
#### All Players tab

- User list - current online players
  - Sorted by:
    - Username ascending
  - Users are added to list when they log in
  - Users are removed from list when they log out
- Message list - list of message for currently selected user

#### Recent Players tab

- A table of the last X number of players you've recently played with
- Ordered by most recent games first
- Three columns: player name, name of game played in, date/time of game
- Online players will appear as enabled
- Offline players will appear as disabled

### User List

  - Selecting/left clicking on a user in this list will show all messages between current user and selected user in the messages list
  - Selecting/left clicking on a user that is online will enable the input text field
  - Selecting/left clicking on a user that is offline will disable the input text field
  - Deselecting a user will disable the input text field
  - Right-clicking on a user in this list will select that user and bring up the global context menu for that user. The functionality of the global context menu is documented elsewhere.

### Messages List

- Contains a list of messages between the current user and another IRC user that is selected in the User List.
- When viewing a conversation and the other user logs out, a message will appear that the other user is now offline.
- When view a conversation and the other user logs in, a message will appear that the other user is now online.

### Receiving a message from another user

Outlines the user experience when the current user receives a private message from another user.

- If the other user is BLOCKED for the current user, ignore the message.
- The "Allow Private Messages Setting" in the Options Window:
  - If set to "All" and user is not blocked, allow message to be received. Else ignore message.
  - If set to "Friends", other user is a friend and is not blocked, allow message to be received. Else, ignore message.
  - If set to "None", ignore message.
- If currently viewing the conversation with the other user, simply add the incoming message to the message list.
- If NOT viewing the conversation with the other user, show notification popup.

### Notification Popup

A small window that shows that a private message has been received from another user.

- If the "Disable Popups from Private Messages" check box is checked in the Options window, the notification popup will never appear.
- If the current user is in game, the popup will not appear.
- Left clicking on the popup will open the PMW to the conversation with the other user in the Messages tab. User will be selected in user list. Messages with other user will appear in messages list.


