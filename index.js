const discord = require('discord.js');
const fs = require('fs');

const COMMAND_TOKEN = '.'
const config = JSON.parse(fs.readFileSync('config.json', 'utf8'));
const client = new discord.Client();
let nextDateToHighlight = new Date();


//=============================================================================
// Helper functions
//=============================================================================

function log(msg) {
    console.log(`>>> ${msg}`);
}

function isAuthorBot(msg) {
    return msg.author.id === config.botID
}

function isFromGuild(msg) {
    return msg.guild.id === config.guildID;
}

function isCommand(msg) {
    return msg.content.startsWith(COMMAND_TOKEN)
}

function getChannel(channelID) {
    return client.channels.get(channelID);
}

//=============================================================================
// Highlight management
//=============================================================================

function findHighlightChannel(guild) {
    return guild.channels.find(c => c.id === config.highlight.channelID);
}

/**
 * Gets the role from the name provided (case insensitive). If it finds one,
 * then it will check if the role is highlightable based on the config setting.
 *
 * @param roleName The name of the role.
 * @param roles The roles on the guild.
 * @returns {null|*} The role, or null if no role matches/exists.
 */
function findHighlightableRole(roleName, roles) {
    let role = findRoleFromName(roleName, roles);

    if (role) {
        const allowedToBeHighlighted = config.highlight.roles.includes(role.id);
        return allowedToBeHighlighted ? role : null;
    }

    return null;
}

/**
 * Returns an object which contains highlight information. It can be used for
 * debugging of the delay.
 *
 * @returns {{seconds: number, canHighlight: boolean, minutes: number}}
 */
function getTimeUntilHighlight() {
    const deltaDate = nextDateToHighlight - new Date();
    const seconds = Math.floor((deltaDate / 1000) % 60);
    const minutes = Math.floor(deltaDate / (60 * 1000));
    const canHighlight = deltaDate <= 0;

    return {
        canHighlight: canHighlight,
        seconds: seconds,
        minutes: minutes,
    }
}

/**
 * Requests a highlight from a user (author) in some channel. The tokens
 * should originate from the channel. It will only allow highlights to occur
 * within a certain amount of time based on the config highlight timeout span
 * amount.
 *
 * @param tokens The split tokens from the command message. This should be
 * split on spaces, such that a message like ".command hi there" would have
 * the following: ["command", "hi", "there"]. This assumes a period is the
 * command token.
 * @param msg The message object.
 */
function requestHighlight(tokens, msg) {
    if (tokens.length < 3) {
        sendChannelMessage(channel, `Usage: .highlight <role> <message> (ex: \`.highlight practice Help BST practice!\`)`);
        return;
    }

    const channel = msg.channel;
    const timeObj = getTimeUntilHighlight();

    if (!timeObj.canHighlight) {
        sendChannelMessage(channel, `Someone has already highlighted recently. Please wait ${timeObj.minutes} minutes and ${timeObj.seconds} seconds before trying again!`);
        return;
    }

    if (channel.id !== config.highlight.channelID) {
        const highlightChannel = findHighlightChannel(channel.guild);
        sendChannelMessage(channel, `Please highlight in the channel designated for highlights: ${highlightChannel}`);
        return;
    }

    let role = findHighlightableRole(tokens[1], channel.guild.roles);

    const member = msg.member;

    if (role) {
        role.setMentionable(true, 'Enabling for bot to highlight').then(updatedRole => {
            const userMessage = tokens.slice(2).join(' ');
            log(`\`${member.displayName}\` highlighting with reason: ${userMessage}`);

            channel.send(`${role} (from ${member.displayName}): ${userMessage}`).then(() => {
                nextDateToHighlight = new Date(new Date().getTime() + config.highlight.timeoutMillis);
                setTimeout(() => highlightThrottle = false, config.highlight.timeoutMillis);

                updatedRole.setMentionable(false, 'Disabling so no one can highlight').then(() => {
                    msg.delete().catch(e => {
                        log(`Unexpected error when deleting highlight message ${msg.id}: ${e}`);
                    });
                }).catch(e => {
                    log(`Unexpected error when setting highlight role ${role.id} to not being mentionable: ${e}`);
                });
            }).catch((e) => {
                log(`Unable to send highlight notification to highlight channel: ${e}`);
            });
        }).catch((e) => {
            log(`Unable to toggle role (ID: ${role.id}): ${e}`);
        });
    } else {
        sendChannelMessage(channel, `Role either does not exist or is not allowed to be highlighted.`);
    }
}


//=============================================================================
// Message handling
//=============================================================================

/**
 * Gets the tokens from a command message.
 * Ex: `.command  hello   hi ` -> ['command', 'hello', 'hi']
 * @param msg The message from the server.
 * @returns {string[]} The list of strings from the broken up command. This
 * does not contain the command token.
 */
function getTokens(msg) {
    return msg.content.slice(COMMAND_TOKEN.length).split(' ').filter(t => t !== '');
}

function addJoinMemberRoles(member) {
    // TODO
}

function handleDirectMessage(msg) {
    if (isAuthorBot(msg)) {
        return;
    }

    // TODO
}

function handleChannelMessage(msg) {
    if (isAuthorBot(msg) || !isFromGuild(msg) || !isCommand(msg)) {
        return;
    }

    const tokens = getTokens(msg);
    if (tokens.length === 0) {
        return;
    }

    switch (tokens[0].toUpperCase()) {
        case 'HIGHLIGHT':
            requestHighlight(tokens, msg, msg.channel);
            break;
        default:
            break;
    }
}


//=============================================================================
// Core
//=============================================================================

client.on('message', msg => {
    if (msg.channel.type === 'dm') {
        handleDirectMessage(msg);
    } else if (msg.channel.type === 'text' && msg.guild.id === config.guildID) {
        handleChannelMessage(msg);
    }
});

client.on('ready', () => {
    console.log('Connected to Discord server!')
});

client.on('guildMemberAdd', member => {
    addJoinMemberRoles(member);

    if (config.joinMessage.length > 0) {
        member.send(config.joinMessage).then(() => {
            // This means it was sent, and all is good.
        }).catch(err => {
            log(`ERROR: Unable to send greeting message to new member '${member.username}' (ID: ${member.id}): ${err}`);
        });
    }
});

client.login(config.auth).then(() => {
    setDebugChannel();
}).catch(err => {
    throw `Failed to log into the bot: ${err}`;
});