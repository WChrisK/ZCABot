# ZCABot

### Usage

Create a file called `.auth` in the working directory and put your bot token in it (it must only contain the text, no newlines or spaces).

Then run it.

### Helper Script

There is some underlying issue where the bot can die every two months or so due to some async issue with Discord .NET, and the hacky solution was as follows:

```bash
#!/bin/bash

# We put this shell script in the directory above the source project
cd ZCABot

dotnet build
while true; do
	dotnet run
	echo "--------------------------------------"
	echo "BOT CRASHED, RESTARTING..."
	echo "--------------------------------------"
	sleep 5
done

```
