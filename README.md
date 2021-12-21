# FridgeBot
*This is so good we'll put it on the fridge.*

FridgeBot is basically Starboard except it can do an unlimited amount of emotes per server, eliminating the need to use multiple bots.

## Docker deployment
0. Create a bot user. I'm not explaining that here. The bot user might need to have all privileged gateway intents, I have been too lazy to find out. Let me know if it works without them.
1. Copy the files postgres.env.example and fridgebot.env.example to postgres.env and fridgebot.env, respectively.
2. Add your bot user token to fridgebot.env
3. If using the postgresql container provided by docker-compose.yml, no further changes are necessary. Otherwise modify the connection string and possibly the connection mode. Note that only Sqlite and Postgres is supported and the migrations in this repository are for Postgres, so if you want anything else you will need to get your hands a little dirty. Look up how to add migrations to entity framework for more info.
4. `docker-compose up`

## Usage
Note that all of these commands require the user to have Administrator rights over the server.

After joining the bot to your server, the first thing you should to is `@FridgeBot init` followed by the ID of the channel you want messages to be featured in.

Then, for each emote you want to use, send `@FridgeBot emote &lt;the emote&gt; &lt;minimum amount to add&gt; &lt;maximum amount to remove&gt;`
When the number of reactions on a message using that emote exceed the first number, the message will be featured in the channel you set. When it drops below the second number, the message will be removed from the channel (unless there are other emotes that make it eligible for featuring).

If you ever want to change either number simply re-run the command.

To stop using an emote, send `@FridgeBot delete &lt;the emote&gt;`. Note that existing fridge messages will not be updated automatically, however the emote will be removed the next time the message is updated when the reaction count on the original message changes.

To make sure that a certain channel is ignored by FridgeBot, simply deny it the permission it needs to read that channel's messages.
