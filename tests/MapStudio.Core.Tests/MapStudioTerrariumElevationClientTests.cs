using MapStudio.Core.Generation.Terrain;
using Xunit;

namespace MapStudio.Core.Tests;
public sealed class MapStudioTerrariumElevationClientTests
{
    [Theory]
    [InlineData(128,0,0,0)]
    [InlineData(128,100,128,100.5)]
    [InlineData(127,255,0,-1)]
    public void DecodeRgb(byte r,byte g,byte b,double expected) =>
        Assert.Equal(expected,MapStudioTerrariumElevationClient.DecodeHeight(r,g,b),6);
    [Fact]
    public void EquatorMapsToCenterPixelBoundary()
    {
        var p=MapStudioTerrariumElevationClient.PixelCoordinates(0,0,1);
        Assert.Equal(255.5,p.X,6);
        Assert.Equal(255.5,p.Y,6);
    }
    [Fact]
    public void RejectsInvalidLatitude() =>
        Assert.Throws<InvalidDataException>(()=>MapStudioTerrariumElevationClient.PixelCoordinates(90,0,15));
    [Fact]
    public async Task SamplesAcrossTileBoundaryAndReusesDownloads()
    {
        using var handler=new TileHandler();
        using var http=new HttpClient(handler);
        var client=new MapStudioTerrariumElevationClient(http,_ =>
        {
            var rgba=new byte[256*256*4];
            for(int i=0;i<rgba.Length;i+=4){rgba[i]=128;rgba[i+1]=100;rgba[i+3]=255;}
            return rgba;
        });
        var surface=await client.DownloadAsync(-0.00001,-0.00001,0.00001,0.00001,3,3);
        Assert.All(surface.Grid.Elevations,h=>Assert.Equal(100,h,6));
        Assert.Equal(4,handler.Requests);
    }
    private sealed class TileHandler : HttpMessageHandler
    {
        public int Requests {get;private set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Requests++;
            Assert.Contains("/terrarium/15/",request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new ByteArrayContent([1])});
        }
    }
}
