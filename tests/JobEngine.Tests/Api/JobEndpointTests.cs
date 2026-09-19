using System.Net;
using System.Net.Http.Json;
using JobEngine.Core;
using JobEngine.Tests.Infrastructure;
using JobEngine.Worker.Api;

namespace JobEngine.Tests.Api;

public class JobEndpointTests : ApiTestBase, IClassFixture<PostgresFixture>
{
    public JobEndpointTests(PostgresFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListJobs_ReturnsEmptyPage_WhenNoJobs()
    {
        var page = await Client
            .GetFromJsonAsync<PagedResponse<JobSummaryResponse>>("/api/jobs");

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ListJobs_FiltersByStatus()
    {
        await SeedJobsAsync(
            NewJob(status: JobStatus.Pending),
            NewJob(status: JobStatus.Succeeded),
            NewJob(status: JobStatus.Succeeded));

        var page = await Client.GetFromJsonAsync<PagedResponse<JobSummaryResponse>>(
            "/api/jobs?status=succeeded");

        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, j => Assert.Equal("Succeeded", j.Status));
    }

    [Fact]
    public async Task ListJobs_FiltersByType()
    {
        await SeedJobsAsync(
            NewJob(type: "delayed-greeting"),
            NewJob(type: "flaky"));

        var page = await Client.GetFromJsonAsync<PagedResponse<JobSummaryResponse>>(
            "/api/jobs?type=flaky");

        Assert.NotNull(page);
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public async Task ListJobs_Paginates()
    {
        var jobs = Enumerable.Range(0, 7).Select(_ => NewJob()).ToArray();
        await SeedJobsAsync(jobs);

        var first = await Client.GetFromJsonAsync<PagedResponse<JobSummaryResponse>>(
            "/api/jobs?page=1&pageSize=3");

        var last = await Client.GetFromJsonAsync<PagedResponse<JobSummaryResponse>>(
            "/api/jobs?page=3&pageSize=3");

        Assert.NotNull(first);
        Assert.NotNull(last);

        Assert.Equal(3, first.Items.Count);
        Assert.Single(last.Items);
        Assert.Equal(7, first.TotalCount);
        Assert.Equal(3, first.TotalPages);
    }

    [Fact]
    public async Task ListJobs_RejectsUnknownStatus()
    {
        var response = await Client.GetAsync("/api/jobs?status=nonsense");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task ListJobs_RejectsInvalidPaging(string query)
    {
        var response = await Client.GetAsync($"/api/jobs{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetJob_ReturnsNotFound_WhenMissing()
    {
        var response = await Client.GetAsync("/api/jobs/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Stats_IncludesEveryStatus_EvenWhenZero()
    {
        await SeedJobsAsync(NewJob(status: JobStatus.Succeeded));

        var stats = await Client.GetFromJsonAsync<StatsResponse>("/api/stats");

        Assert.NotNull(stats);
        Assert.Equal(5, stats.CountsByStatus.Count);
        Assert.Equal(1, stats.CountsByStatus["Succeeded"]);
        Assert.Equal(0, stats.CountsByStatus["DeadLetter"]);
        Assert.Equal(1, stats.TotalJobs);
    }
}