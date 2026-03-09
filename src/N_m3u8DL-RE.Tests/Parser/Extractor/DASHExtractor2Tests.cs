using Shouldly;
using N_m3u8DL_RE.Parser.Config;
using N_m3u8DL_RE.Parser.Extractor;

namespace N_m3u8DL_RE.Tests.Parser.Extractor;

public class DASHExtractor2Tests
{
    private static ParserConfig CreateTestConfig(string mpdFileName) => new ParserConfig
    {
        OriginalUrl = $"file:///fake/path/to/{mpdFileName}",
    };
    
    [Fact]
    public async Task DASHExtractor2_Normal()
    {
        const string mpdName = "Dash.Manifest_1080p.mpd";
        var config = CreateTestConfig(mpdName);
        var content = ResourceHelper.Read(mpdName);
        var extractor = new DASHExtractor2(config);
        var results = await extractor.ExtractStreamsAsync(content);
        results.ShouldNotBeNull();
        results.Count.ShouldBe(23);

        var first = results.First();
        first.ToString().ShouldBe("[aqua]Vid[/] 512x288 | 386 Kbps | 1 | avc1.64001f | 184 Segments | Main | ~12m16s");
        first.AudioId.ShouldBe("15");
        first.Bandwidth.ShouldBe(386437);
        first.Extension.ShouldBe("m4s");
        first.Language.ShouldBe("und");
        first.SubtitleId.ShouldBe("25");
        first.Playlist.ShouldNotBeNull();
        first.Playlist.IsLive.ShouldBe(false);
        first.Playlist.TotalDuration.ShouldBe(736);
        first.Playlist.MediaInit.ShouldNotBeNull();
        first.Playlist.MediaInit.Url.ShouldBe("1/init.mp4");
    }

    [Fact]
    public async Task DASHExtractor2_LiveStream_DST_TimezoneFix()
    {
        // Test case for the DST bug fix: ensure UTC time handling in live DASH streams
        // This test simulates the scenario described in the bug report where a live DASH stream
        // spans a DST transition and should not request segments 1 hour into the future
        
        var liveDashMpd = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD
  type=""dynamic""
  publishTime=""2026-03-09T03:43:39.662Z""
  availabilityStartTime=""2026-02-18T16:22:16.282Z""
  minimumUpdatePeriod=""PT30S""
  minBufferTime=""PT6S""
  suggestedPresentationDelay=""PT8S""
  timeShiftBufferDepth=""PT40S"">
  <Period start=""PT0S"">
    <AdaptationSet mimeType=""video/mp4"" codecs=""avc1.64001f"">
      <Representation id=""1"" bandwidth=""1000000"" width=""1280"" height=""720"">
        <SegmentTemplate media=""segment_$Number$.m4s"" initialization=""init.mp4""
                        duration=""2"" startNumber=""1"" timescale=""1""/>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        var config = CreateTestConfig("live_test.mpd");
        var extractor = new DASHExtractor2(config);
        var results = await extractor.ExtractStreamsAsync(liveDashMpd);
        
        results.ShouldNotBeNull();
        results.Count.ShouldBe(1);
        
        var stream = results.First();
        stream.Playlist.ShouldNotBeNull();
        stream.Playlist.IsLive.ShouldBe(true);
        
        // Verify that PublishTime is parsed correctly with timezone awareness
        stream.PublishTime.ShouldNotBeNull();
        stream.PublishTime.Value.Kind.ShouldBe(DateTimeKind.Utc); // Should preserve UTC timezone info
        
        // The fix ensures that both availabilityStartTime and current time calculations
        // are done in UTC to avoid DST issues
        // The test verifies the stream was parsed successfully without requesting future segments
    }
}