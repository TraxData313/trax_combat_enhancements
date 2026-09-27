using TraxCombat.Core;

namespace TraxCombat.Core.Tests;

public class RateLimiterTests
{
    [Fact]
    public void Burst_passes_then_lines_are_dropped_and_counted()
    {
        var r = new RateLimiter(burst: 3, perSecond: 1);
        for (int i = 0; i < 3; i++)
            Assert.True(r.TryPass("damage", 0, out _));
        Assert.False(r.TryPass("damage", 0, out _));
        Assert.False(r.TryPass("damage", 0.5, out _));
        Assert.Equal(2, r.PendingSuppressed("damage"));
    }

    [Fact]
    public void Tokens_refill_with_time_and_the_next_line_reports_what_was_dropped()
    {
        var r = new RateLimiter(burst: 1, perSecond: 2);
        Assert.True(r.TryPass("blow", 0, out _));
        Assert.False(r.TryPass("blow", 0.1, out _));
        Assert.False(r.TryPass("blow", 0.2, out _));

        Assert.True(r.TryPass("blow", 0.6, out int suppressed));
        Assert.Equal(2, suppressed);
        Assert.Equal(0, r.PendingSuppressed("blow"));
    }

    [Fact]
    public void Refill_never_exceeds_the_burst()
    {
        var r = new RateLimiter(burst: 2, perSecond: 10);
        Assert.True(r.TryPass("x", 0, out _));
        // A long quiet spell refills to the burst size, not beyond.
        int passed = 0;
        for (int i = 0; i < 10; i++)
            if (r.TryPass("x", 1000, out _)) passed++;
        Assert.Equal(2, passed);
    }

    [Fact]
    public void Tags_are_independent()
    {
        var r = new RateLimiter(burst: 1, perSecond: 0);
        Assert.True(r.TryPass("damage", 0, out _));
        Assert.False(r.TryPass("damage", 0, out _));
        Assert.True(r.TryPass("athletics", 0, out _));
    }

    [Fact]
    public void A_flood_of_a_thousand_agents_is_capped()
    {
        // One second of a big battle: 5000 roll lines offered, a handful written.
        var r = new RateLimiter(burst: 40, perSecond: 20);
        int written = 0;
        for (int i = 0; i < 5000; i++)
            if (r.TryPass("damage", i / 5000.0, out _)) written++;
        Assert.InRange(written, 40, 61);
    }

    [Fact]
    public void Drain_reports_and_resets_the_counts()
    {
        var r = new RateLimiter(burst: 1, perSecond: 0);
        r.TryPass("a", 0, out _);
        r.TryPass("a", 0, out _);
        r.TryPass("a", 0, out _);
        var drained = r.DrainSuppressed();
        var entry = Assert.Single(drained);
        Assert.Equal("a", entry.Key);
        Assert.Equal(2, entry.Value);
        Assert.Empty(r.DrainSuppressed());
    }

    [Fact]
    public void Bad_arguments_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RateLimiter(1, -1));
    }

    // Review R8 (step 10b): the hot paths ask Peek before building a verbose line.

    [Fact]
    public void Peek_takes_no_token_while_there_is_room()
    {
        var r = new RateLimiter(burst: 2, perSecond: 0);
        Assert.True(r.Peek("blow", 0)); // a new bucket starts full
        Assert.True(r.Peek("blow", 0));
        Assert.True(r.TryPass("blow", 0, out _));
        Assert.True(r.Peek("blow", 0));
        Assert.True(r.TryPass("blow", 0, out _));
        Assert.Equal(0, r.PendingSuppressed("blow"));
    }

    [Fact]
    public void Peek_without_room_counts_the_line_as_suppressed_like_a_dropped_one()
    {
        var r = new RateLimiter(burst: 1, perSecond: 1);
        Assert.True(r.TryPass("blow", 0, out _));
        Assert.False(r.Peek("blow", 0.1));   // not built: counted
        Assert.False(r.TryPass("blow", 0.2, out _)); // built and dropped: counted the same way
        Assert.Equal(2, r.PendingSuppressed("blow"));

        // The next line that passes reports both, and Peek saw the refill coming.
        Assert.True(r.Peek("blow", 1.1));
        Assert.True(r.TryPass("blow", 1.1, out int suppressed));
        Assert.Equal(2, suppressed);
    }

    [Fact]
    public void Peek_does_not_move_the_refill_clock()
    {
        var r = new RateLimiter(burst: 1, perSecond: 1);
        Assert.True(r.TryPass("x", 0, out _));
        for (int i = 1; i <= 9; i++) Assert.False(r.Peek("x", i * 0.1));
        // The token refilled over the whole second, peeks or not.
        Assert.True(r.TryPass("x", 1.0, out int suppressed));
        Assert.Equal(9, suppressed);
    }
}
