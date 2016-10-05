# DiscordPoGoSniper

A Pokemon Go sniper bot that acts as a Discord chat bot used for verifying posted coordinates by other players.

**Running this code might be against Niantic's TOS**

**This code is a fully functioning prototype; However, it is not actively maintained anymore YMMV**

# Description

The bot listens on specified Discord channels and verifies any posted pokemon sighting coordinates then posts back verified data to desired channels. 
It uses an unofficial third party api to verify if the pokemon does exist, its despawn time and various other attributes that are not available in the official client (IV%, attack moves).
It uses the same method as a sniper bot except it does not actually catch any pokemons.

It can use multiple PTC accounts concurrently to handle bigger loads. Tested on a 10k user channel running 10 PTC accounts.

[![Screenshot 11](https://raw.githubusercontent.com/pingec/DiscordPoGoSniper/master/images/PoGoDiscord_320.png)](https://raw.githubusercontent.com/pingec/DiscordPoGoSniper/master/images/PoGoDiscord.png)
[![Screenshot 2](https://raw.githubusercontent.com/pingec/DiscordPoGoSniper/master/images/PoGoDiscord2_320.png)](https://raw.githubusercontent.com/pingec/DiscordPoGoSniper/master/images/PoGoDiscord2.png)

# Setup

1. Create a \DiscordPoGoSniper\BotConfig.json use \DiscordPoGoSniper\BotConfig.json.example as a template 
1. Make sure the file gets copied over to the build directory
1. Run the solution

# License

```
This software is built on top of many other libs, some of them are my own forks (PoGoLib, PoGoProto, Deiscord.Net).
Thanks to all the authors for their work.

If you use this code, extend it or use this bot as reference for your own implementation,
please give credits to the initial implementation to pingec@pingec.si
If this helped you in any way, leave a star in the github repo https://github.com/pingec/DiscordPoGoSniper

Thank you, have fun.
```

