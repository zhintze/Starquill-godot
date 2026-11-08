using NUnit.Framework;
using Starquill.Display;
using Starquill.Studio;
using UnityEngine;

namespace Starquill.Tests.Studio
{
    public class StudioPixelMatchTests
    {
        [Test]
        public void StudioRender_MatchesTheGameRender()
        {
            var data = StudioTestRepo.Data;
            var s = new RandomSettings { FacialHair = 1f, FacialDetail = 1f };
            var c = new StudioRoller(data).RollCharacter(new[] { "human" }, s, new System.Random(42));
            var species = data.Registry.Species["human"];

            var gameGo = new GameObject("GameRender");
            var game = gameGo.AddComponent<CharacterDisplay>();
            game.Initialize(new ImageResolver(), 400);
            game.RebuildFromData(c.Body, species, c.ToDisplayList(), new DisplayBuilder(data.Registry));
            var gamePx = StudioRenderer.ReadPixels(game.Texture);

            using var source = new DiskSpriteSource(StudioTestRepo.Root);
            using var studio = new StudioRenderer(data, new ImageResolver(source));
            var studioPx = StudioRenderer.ReadPixels(studio.Render(c, 400));

            int opaque = 0, differ = 0, maxDiff = 0;
            for (int i = 0; i < gamePx.Length; i++)
            {
                if (gamePx[i].a > 0) opaque++;
                int d = Mathf.Max(Mathf.Abs(gamePx[i].r - studioPx[i].r), Mathf.Abs(gamePx[i].g - studioPx[i].g),
                                  Mathf.Abs(gamePx[i].b - studioPx[i].b), Mathf.Abs(gamePx[i].a - studioPx[i].a));
                if (d > 2) differ++;
                if (d > maxDiff) maxDiff = d;
            }
            TestContext.WriteLine($"opaque {opaque}, differ(>2) {differ} ({100f * differ / gamePx.Length:0.00}%), max diff {maxDiff}");
            Object.DestroyImmediate(gameGo);

            Assert.Greater(opaque, 1000, "the game render drew a character");
            Assert.LessOrEqual(differ, gamePx.Length / 100, "more than 1% of pixels differ");
        }
    }
}
