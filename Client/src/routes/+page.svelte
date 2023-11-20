<script>
    import {baseUrl} from '../constants.js';
    import debounce from "just-debounce-it";
    import {onMount} from "svelte";
    import BlogPost from "../parts/blog-post.svelte";
    import BlogPostSkeleton from "../parts/blog-post-skeleton.svelte";
    import '../styles/global.css';

    console.log('BASE_URL', baseUrl);

    performance.setResourceTimingBufferSize(100);

    let abortController = null;

    async function filterResults(search = "") {
        page = 1;
        if (scrollElement) {
            scrollElement.scrollTop = 0;
        }
        if (performance.getEntries().length > 50) {
            performance.clearResourceTimings();
        }
        isLoading = true;

        try {
            // trim the search query
            search = search.trim();
            if (search !== "" && search === lastSearchQuery) {
                return lastPosts;
            }
            lastSearchQuery = search;
            if (abortController) {
                abortController.abort();
            }
            const url = `https://${baseUrl}/api/blog/search?query=${encodeURIComponent(searchQuery)}`;
            abortController = new AbortController();
            const res = await fetch(url, {
                signal: abortController.signal,
            });
            const payload = await res.json();
            const performanceEntries = performance.getEntriesByName(url)
            totalDurationInMs = Math.round(performanceEntries[performanceEntries.length - 1]?.duration ?? 0);

            if (payload.stats) {
                serverDurationInMs = Math.max(0, payload.stats.durationInMs);
                if (serverDurationInMs > totalDurationInMs) {
                    serverDurationInMs = 0;
                }
                totalResults = payload.stats.totalResults;
            } else {
                serverDurationInMs = null;
                totalResults = null;
            }
            // check if posts is a list of objects or a list of strings
            if (!payload.data || payload.data.length === 0) {
                suggestions = [];
                lastPosts = [];
            } else if (payload.data[0].id) {
                suggestions = [];
                lastPosts = payload.data.map((posts) => {
                    return {
                        id: posts.id,
                        title: posts.title,
                        link: posts.link,
                        authors: posts.authors,
                        date: new Date(posts.date),
                        categories: posts.categories,
                        content: posts.content ?? posts.subtitle,
                        blogId: posts.blogId,
                        company: posts.company,
                        logoName: posts.logoName,
                    };
                });
            } else {
                suggestions = payload.data;
                lastPosts = [];
            }
        } catch (err) {
            if (err.name === "AbortError") {
                console.log('Fetch aborted:', searchQuery);
                return;
            }
            suggestions = [];
            lastPosts = [];
            serverDurationInMs = null;
            totalResults = null;
            throw err;
        } finally {
            isLoading = false;
        }
        return lastPosts;
    }

    let currentlySearching = false;
    let pendingSearch = false;
    let isLoading = false;
    let searchQuery = "";
    let lastSearchQuery = "";
    let lastPosts = [];
    let serverDurationInMs = null;
    let totalDurationInMs = null;
    let totalResults = null;
    let suggestions = [];
    let searchedPosts = Promise.resolve([]);
    let page = 1;
    let scrollElement;

    let showSkeleton = true;
    let timeout;

    function resetSkeleton() {
        showSkeleton = false;
        if (timeout) {
            clearTimeout(timeout);
        }
        timeout = setTimeout(() => {
            showSkeleton = true;
        }, 250);
    }

    onMount(async () => {
        searchedPosts = filterResults(searchQuery);
    });

    const doSearch = debounce(
        () => {
            resetSkeleton();
            searchedPosts = filterResults(searchQuery);
        },
        50,
        true
    );

    // Consider using https://www.npmjs.com/package/overlayscrollbars-svelte
    function infiniteScroll(node) {
        node.addEventListener("scroll", async () => {
            if (
                node.scrollTop + node.clientHeight * 3 >=
                node.scrollHeight
            ) {
                if (isLoading || !totalResults || totalResults <= await searchedPosts.then((posts) => posts.length)) {
                    return;
                }

                ++page;
                performance.clearResourceTimings();
                isLoading = true;

                try {
                    if (abortController) {
                        abortController.abort();
                    }
                    const url = `https://${baseUrl}/api/blog/search?query=${encodeURIComponent(searchQuery)}&page=${page}`;
                    abortController = new AbortController();
                    const res = await fetch(url, {
                        signal: abortController.signal,
                    });
                    const payload = await res.json();
                    const performanceEntries = performance.getEntriesByName(url)
                    totalDurationInMs = Math.round(performanceEntries[performanceEntries.length - 1]?.duration ?? 0);

                    if (payload.stats) {
                        serverDurationInMs = Math.max(0, payload.stats.durationInMs);
                        if (serverDurationInMs > totalDurationInMs) {
                            serverDurationInMs = 0;
                        }
                        totalResults = payload.stats.totalResults;
                    } else {
                        serverDurationInMs = null;
                        totalResults = null;
                    }
                    // check if posts is a list of objects or a list of strings
                    let nextPosts = payload.data.map((posts) => {
                        return {
                            id: posts.id,
                            title: posts.title,
                            link: posts.link,
                            authors: posts.authors,
                            date: new Date(posts.date),
                            categories: posts.categories,
                            content: posts.content ?? posts.subtitle,
                            blogId: posts.blogId,
                            company: posts.company,
                            logoName: posts.logoName,
                        };
                    });
                    // let nextPosts = await loadMore();
                    if (nextPosts.length > 0) {
                        searchedPosts = searchedPosts.then((posts) => {
                            return posts.concat(nextPosts);
                        });
                    }
                } catch (err) {
                    if (err.name === "AbortError") {
                        console.log('Fetch aborted:', searchQuery);
                        return;
                    }
                    suggestions = [];
                    lastPosts = [];
                    serverDurationInMs = null;
                    totalResults = null;
                    throw err;
                } finally {
                    isLoading = false;
                }
            }
        });
    }

</script>

<div class="results-container">
    <div class="column-wrapper">
        <div class="column-wrapper-contents">
            <div class="site-wrapper" style="display: flex; align-items: center; gap: 0.5rem;">
                <img class="site-logo" src="./sparkify.png" width="64px" alt="Sparkify Logo"/>
                <span>/</span>
                <h1 class="site-title">Software Insights</h1>
            </div>
            <div class="search-wrapper">
                <input
                        type="search"
                        id="search"
                        placeholder="Search"
                        autocomplete="off"
                        bind:value={searchQuery}
                        on:input={doSearch}
                />
                <div class="search-statistics">
                    {#if serverDurationInMs !== null}
                        <span>server: {serverDurationInMs} ms</span>
                        <span class="dimmer">&nbsp;|&nbsp;</span>
                        <span>round trip: {totalDurationInMs} ms</span>
                    {:else}
                        <span>server: -- ms</span>
                        <span class="dimmer">&nbsp;|&nbsp;</span>
                        <span>round trip: -- ms</span>
                    {/if}

                </div>
            </div>
            <div class="search-summary">
                {#if totalResults}
      <span
      >{totalResults} articles
          {#if searchQuery}found{/if}</span
      >
                {:else}
                    <span>0 articles found</span>
                {/if}

            </div>
        </div>
    </div>
    <div class="padding-wrapper" bind:this={scrollElement} use:infiniteScroll>
        <div class="search-results">
            {#await searchedPosts}
                {#if lastPosts.length !== 0}
                    {#each lastPosts as post}
                        <BlogPost {post} {searchQuery}/>
                    {/each}
                {:else if showSkeleton}
                    <BlogPostSkeleton/>
                    <BlogPostSkeleton/>
                    <BlogPostSkeleton/>
                    <BlogPostSkeleton/>
                    <BlogPostSkeleton/>
                    <BlogPostSkeleton/>
                {/if}
            {:then posts}
                {#if posts.length > 0}
                    {#each posts as post}
                        <BlogPost {post} {searchQuery}/>

                    {/each}
                {:else if suggestions.length > 0 && lastPosts.length === 0}
                    <div class="search-suggestions">
                        <div>Did you mean?</div>
                        <br/>
                        {#each suggestions as suggestion}
                            <a
                                    href={suggestion.link}
                                    on:click={() => {
            searchQuery = suggestion.title;
            doSearch();
          }}
                                    aria-label="Search using this suggestion"
                            >
                                {suggestion.title}
                            </a>
                            <br/>
                        {/each}
                    </div>
                {/if}
            {:catch error}
                <p style="color: rgb(128 128 128 / 80%)">The server is not responding.</p>
            {/await}
        </div>
    </div>
</div>
