# SantanderCodingTest

1 - To run: either run integration tests in SantanderCodingTestTests project, or start SantanderCodingTest in swagger. Subsequent calls in swagger are much faster once the cache is populated from the first run.

2 - I have assumed the story scores do not change frequently (perhaps daily) but the call to the API as suggested in the spec would be frequent. Therefore, I wrote the HackerNews API calls and underlying caching with that use case in mind - i.e. Load all news items on first request, then read from cache thereafter until cache expires

3 - Understanding the expected traffic and the likely use of the n value (how varied n value would be in a typical cached period) would have assisted in deciding whether to also cache at the API output level (which is even faster though coulud bloat cache memory) - see comment in Controller

4 - One enhancement that I started looking into was the ability to configure max concurrent requests to the HAckerNews api. Again, understanding real world usage and performance metrics in those scenarios would have been required to properly implement. 

5 - Another enhancement would have been to have the cache pre-populated by a seperate process/services with the cache then shared between n number of services/microservices that were providing the API interface

5 - Note I have used HdrHistogram nuget package to output performance stats for some of the integration tests. http://www.hdrhistogram.org/ - which allows an understanding of percentile API call performance. You will see the output in the Text Explorer window. This confirms the vast majority of API calls are extremely fast (999 out of 1000 calls – 99.9th percentile for example) and the first call that populates the cache is slower. During development, this output highlighted a logical flaw in my Service code where multiple calls from my integration test were trying to populate the cache (and therefore calling Hacker News API for no reason), and resulted in me implementing a lock – see comments. Therefore, for a given period of the cache, only one call (the first) will ever hit the HackerNews api (see point 4 as a possible refactor). The value column in the output is in microseconds so with appropriate cloud scalling, in theory based on these stats the API could handle millions of calls a second, and importantly prove that with hard evidence prior to production deployment.
