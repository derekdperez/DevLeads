namespace DevLeads.Core.Entities;

/// <summary>
/// One Discord text channel the operator's bot can see. Channels are discovered from the
/// servers the bot was invited to (never from the operator's personal account — Discord
/// bans user-account automation). Monitored channels are polled for replies to the
/// operator's tracked posts and for mentions; posting targets one of these channels.
/// </summary>
public class DiscordChannel
{
    public long Id { get; set; }

    /// <summary>Discord snowflake id of the server (guild).</summary>
    public string GuildId { get; set; } = "";
    public string GuildName { get; set; } = "";

    /// <summary>Discord snowflake id of the channel — the API identity everything keys on.</summary>
    public string ChannelId { get; set; } = "";
    public string ChannelName { get; set; } = "";

    /// <summary>When true, engagement sync polls this channel for replies and mentions.</summary>
    public bool MonitorEnabled { get; set; }

    /// <summary>
    /// Newest message snowflake already processed, so each sync fetches only what came
    /// after it. Empty until the first sync (which seeds the cursor from recent history).
    /// </summary>
    public string LastSyncedMessageId { get; set; } = "";
    public DateTimeOffset? LastSyncedAt { get; set; }

    /// <summary>Operator notes: channel rules, whether promos are allowed, cadence…</summary>
    public string Notes { get; set; } = "";

    /// <summary>Channel rows the latest server sync no longer saw (bot kicked, channel gone).</summary>
    public bool Stale { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
