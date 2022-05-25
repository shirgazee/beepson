## Features:

- Time zone configuration
  
- 12h time support:
  - 11:30am #lunch
  - 6:30 pm #home
  - 12pm

- 24h time support:
  - 12:30 #call someone special
  - 22:30 #sleep
  - 11:54", "23 (hours)

- Time span text support:
    - 1h 30m #study
    - 30s
    - 90s #do stuff

## Deploying

1. Clone this repository
```
git clone https://github.com/shirgazee/beepson
```
2. Obtain telegram bot token from [BotFather](https://t.me/botfather)
3. Change bot token in docker-compose:
```
environment:
      BEEPSON_TOKEN: "👉 here"
```
4. Launch (flags are useful after update via `git pull`)
```
docker-compose up -d --build --force-recreate
```