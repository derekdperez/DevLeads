namespace DevLeads.Core.Platforms;

/// <summary>Seed definition for a platform-presence catalog entry.</summary>
public record PlatformSeed(
    string Key,
    string Name,
    string Url,
    string SignupUrl,
    string Category,
    string Audience,
    string Rationale,
    string PostingNotes,
    string CostModel,
    bool RequiresResume = false);

/// <summary>
/// The curated starter catalog of platforms where a solo consultant can win paid work or
/// build reputation. Seeded add-only (by Key); the operator owns entries afterwards, and
/// AI discovery appends beyond this list. Keys double as OperatorPost.Platform values.
/// </summary>
public static class DefaultPlatformCatalog
{
    public static readonly PlatformSeed[] All =
    {
        new("reddit", "Reddit hiring subreddits", "https://www.reddit.com/r/forhire/", "https://www.reddit.com/register/",
            "hiring board", "Small businesses and individuals posting [Hiring] and browsing [For Hire] threads (r/forhire, r/HireADeveloper, r/jobbit).",
            "Direct route to small paid engagements; also the community channel where helpful comments build the account's credibility.",
            "[For Hire] titles must be specific; a rate is required on r/forhire; repost roughly monthly; answering [Hiring] posts fast beats a good profile.",
            "free"),

        new("upwork", "Upwork", "https://www.upwork.com/", "https://www.upwork.com/signup/",
            "freelance marketplace", "Businesses of all sizes buying freelance software work; heavy competition, but budgets are real.",
            "The largest freelance marketplace; a strong niche profile (rescue/modernization) plus fast first proposals can bootstrap reviews.",
            "The first two sentences of the profile carry the search preview; proposals must reference the client's actual problem in the first line.",
            "commission"),

        new("linkedin", "LinkedIn", "https://www.linkedin.com/", "https://www.linkedin.com/signup/",
            "social", "Professional network — founders, CTOs, agency owners, recruiters.",
            "Where past colleagues and warm referrals live; availability posts and consistent commentary compound into inbound work.",
            "Post 1-3x/week max; hook in the first line (feed truncates); comments on others' posts often outperform own posts for visibility.",
            "free"),

        new("craigslist", "Craigslist (computer services)", "https://www.craigslist.org/", "https://accounts.craigslist.org/login/signup",
            "local", "Local small businesses in Western MA needing websites fixed, systems un-broken, data recovered.",
            "Near-zero competition from senior engineers locally; small paid fixes that become retainer relationships.",
            "Plain-spoken services ad, no jargon; renew/repost every few weeks; local trust signals (town names) matter more than stack lists.",
            "free"),

        new("hackernews", "Hacker News", "https://news.ycombinator.com/", "https://news.ycombinator.com/login",
            "developer community", "Founders, engineers, and technical decision-makers.",
            "The monthly 'Freelancer? Seeking freelancer?' thread is read by funded founders; substantive comments build a recognizable handle.",
            "Post in the monthly freelancer thread (first day, SEEKING WORK format); elsewhere only substantive technical comments — any self-promotion outside the thread burns the account.",
            "free"),

        new("devto", "DEV Community (dev.to)", "https://dev.to/", "https://dev.to/enter",
            "content", "Working developers and the tech leads who read practical posts.",
            "Low-friction home for the content studio's articles; practical war stories (production debugging, migrations) rank well and link back to the operator.",
            "Cross-post blog content with a canonical URL; series on one niche outperform scattered topics; end posts with a one-line availability note, not a pitch.",
            "free"),

        new("hashnode", "Hashnode", "https://hashnode.com/", "https://hashnode.com/onboard",
            "content", "Developers following personal engineering blogs.",
            "Free personal blog on the operator's own (sub)domain — the SEO home base the marketplaces can't take away.",
            "Publish the content studio's long-form pieces here first, then syndicate; consistent monthly cadence beats bursts.",
            "free"),

        new("stackoverflow", "Stack Overflow", "https://stackoverflow.com/", "https://stackoverflow.com/users/signup",
            "developer community", "Every developer who searches an error message; profile links out.",
            "Answering .NET/IIS/EF questions in the operator's exact niche is durable proof of expertise that Google surfaces for years.",
            "Answer new questions in 2-3 watched tags within the first hour; complete, runnable answers; profile filled out with consulting link.",
            "free"),

        new("github", "GitHub", "https://github.com/", "https://github.com/signup",
            "developer community", "Developers and technical founders evaluating who they're about to hire.",
            "The de-facto portfolio: a profile README, a few useful OSS contributions in the niche, and pinned repos convert lookers into contacts.",
            "Profile README stating what you do and how to hire you; contribute fixes to libraries you actually use; quality over volume.",
            "free"),

        new("wellfound", "Wellfound (AngelList Talent)", "https://wellfound.com/", "https://wellfound.com/join",
            "hiring board", "Startups (often funded) hiring engineers, including contract roles.",
            "Direct line to startups that need senior help now and move fast; contract-friendly.",
            "Profile emphasizes outcomes and availability; apply with 2-3 personalized sentences; keep the 'open to contract' flag on.",
            "free",
            RequiresResume: true),

        new("contra", "Contra", "https://contra.com/", "https://contra.com/signup",
            "freelance marketplace", "Startups and creators hiring independents; commission-free.",
            "Zero commission and a portfolio-first format that suits a senior generalist; younger marketplace = less entrenched competition.",
            "Portfolio 'services' framed as fixed-scope offers (e.g. 'production incident rescue'); respond to invites fast.",
            "free"),

        new("braintrust", "Braintrust", "https://www.usebraintrust.com/", "https://app.usebraintrust.com/onboarding/",
            "freelance marketplace", "Enterprises and funded startups hiring vetted senior talent.",
            "Fee-free for talent and skews to exactly this seniority; slower to start but rates are professional.",
            "Complete vetting once; keep availability current; apply early — roles close on the first good applicants.",
            "vetted (free to apply)",
            RequiresResume: true),

        new("gunio", "Gun.io", "https://gun.io/", "https://gun.io/find-work/",
            "freelance marketplace", "US companies hiring vetted freelance developers.",
            "Vetted network that matches work to you — compounds passively once accepted.",
            "Pass the vetting interview; niche positioning (legacy .NET rescue) makes matches likelier than 'full-stack generalist'.",
            "commission",
            RequiresResume: true),

        new("arcdev", "Arc.dev", "https://arc.dev/", "https://arc.dev/developers",
            "freelance marketplace", "Remote-first companies hiring senior developers, contract and full-time.",
            "Remote-only focus fits worldwide availability; vetted pool keeps rates sane.",
            "One thorough profile + code screen; then it's inbound — keep timezone/availability accurate.",
            "vetted (free to apply)",
            RequiresResume: true),

        new("codementor", "Codementor", "https://www.codementor.io/", "https://www.codementor.io/apply-mentor",
            "freelance marketplace", "Developers and teams paying for 1:1 help, code review, and small projects.",
            "Live-help requests ('my production app is down') are literally the emergency-rescue campaign with payment attached.",
            "Watch the live request feed and respond in minutes; strong niche keywords in the mentor profile drive matches.",
            "commission"),

        new("twitter", "X (Twitter) dev community", "https://x.com/", "https://x.com/i/flow/signup",
            "social", "Developers, indie founders, #buildinpublic; fast-moving and referral-rich.",
            "Where dev reputation spreads fastest; threads on real debugging war stories travel and bring DMs.",
            "Short practical threads > links; reply to bigger accounts in the niche; pin an availability tweet.",
            "free"),

        new("bluesky", "Bluesky", "https://bsky.app/", "https://bsky.app/",
            "social", "Growing developer community, heavy .NET/OSS presence since the migration waves.",
            "Early-mover advantage: smaller graph means senior voices get followed quickly.",
            "Same content as X; join dev feeds/starter packs; engagement is conversational, not broadcast.",
            "free"),

        new("mastodon", "Mastodon (dotnet.social)", "https://dotnet.social/", "https://dotnet.social/auth/sign_up",
            "social", ".NET-specific instance; core .NET community members and Microsoft folks are active.",
            "A .NET-native watering hole — exactly the audience that refers modernization work.",
            "Introduce yourself with the #introduction tag; boost and reply generously; hashtags do the discovery work.",
            "free"),

        new("discordcsharp", "C# Discord", "https://discord.gg/csharp", "https://discord.com/register",
            "developer community", "Large C# community: hobbyists to senior engineers, with job/collab channels.",
            "Being visibly helpful in help channels builds the reputation that gets you tagged when paid work appears.",
            "Help in the help channels first; job channels have strict formats — read pins; no drive-by self-promotion.",
            "free"),

        new("indiehackers", "Indie Hackers", "https://www.indiehackers.com/", "https://www.indiehackers.com/sign-up",
            "developer community", "Bootstrapped founders — perpetually short on senior engineering help.",
            "Founders here pay for concrete fixes and part-time senior help; comments on 'my app is struggling' posts convert.",
            "Genuine build-in-public participation; answer technical struggles concretely; profile states consulting availability.",
            "free"),

        new("meetup", "Meetup (Western MA / Boston tech)", "https://www.meetup.com/", "https://www.meetup.com/register/",
            "local", "Local developers, IT managers, and business owners at tech and business meetups.",
            "In-person trust closes local contracts no online profile can; the Pioneer Valley has little senior .NET competition.",
            "Attend regularly before pitching anything; offer a talk ('war stories from production rescues') — speakers get approached.",
            "free"),

        new("fiverr", "Fiverr", "https://www.fiverr.com/", "https://www.fiverr.com/join",
            "freelance marketplace", "Buyers searching for productized fixed-price services.",
            "Productized gigs ('I will fix your broken ASP.NET deployment') capture search demand with zero ongoing effort.",
            "Gig titles must match what buyers type; tiered packages; over-deliver early for the review base.",
            "commission"),

        new("youtube", "YouTube", "https://www.youtube.com/", "https://www.youtube.com/account",
            "content", "Developers and technical managers searching 'how to fix/migrate X'.",
            "Screen-recorded debugging/migration walkthroughs rank for years and pre-sell expertise better than any ad.",
            "10-20 min practical walkthroughs of real (anonymized) problems; titles = the search query; description links to contact.",
            "free"),
    };
}
