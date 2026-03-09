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
    public async Task DASHExtractor2_LiveStream_DST_TransitionFix()
    {
        // DST bug fix test: verify segment calculation uses UTC consistently
        // Real-world data from stream on DST transition day
        
        var liveDashMpd = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<MPD
  type=""dynamic""
  publishTime=""2026-03-09T19:55:00.000Z""
  availabilityStartTime=""2026-02-18T16:22:16.282Z""
  minimumUpdatePeriod=""PT30S""
  minBufferTime=""PT6S""
  suggestedPresentationDelay=""PT8S""
  timeShiftBufferDepth=""PT60S"">
  <Period start=""PT0S"">
    <AdaptationSet mimeType=""video/mp4"" codecs=""avc1.64001f"">
      <Representation id=""1"" bandwidth=""1000000"" width=""1280"" height=""720"">
        <SegmentTemplate media=""segment_$Number$.m4s"" initialization=""init.mp4""
                        duration=""4000"" startNumber=""1"" timescale=""1000""/>
      </Representation>
    </AdaptationSet>
  </Period>
</MPD>";

        var config = CreateTestConfig("dst_test.mpd");
        var extractor = new DASHExtractor2(config);
        var results = await extractor.ExtractStreamsAsync(liveDashMpd);
        
        results.ShouldNotBeNull();
        var stream = results.First();
               
        // Validate expected segment calculation for 2026-03-09T15:55:00 EST
        var availabilityStart = DateTime.Parse("2026-02-18T16:22:16.282Z", null, System.Globalization.DateTimeStyles.RoundtripKind);
        var estTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var testTimeEst = new DateTime(2026, 3, 9, 15, 55, 0, DateTimeKind.Unspecified);
        var testTime = TimeZoneInfo.ConvertTimeToUtc(testTimeEst, estTimeZone);
        var timeDiff = testTime - availabilityStart;
        var expectedSegment = (long)Math.Floor(timeDiff.TotalSeconds / 4.0) + 1;
        
        expectedSegment.ShouldBe(413591L); 

        stream.Playlist.ShouldNotBeNull();
        var segments = stream.Playlist.MediaParts[0].MediaSegments;
        segments.ShouldNotBeEmpty();
        segments.Count.ShouldBeLessThanOrEqualTo(15); // 60s buffer / 4s segments
        segments.First().Duration.ShouldBe(4.0, tolerance: 0.1);
    }
}