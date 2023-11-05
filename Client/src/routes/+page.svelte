<script>
    import debounce from "just-debounce-it";
    import {onMount} from "svelte";
    import BlogPost from "../parts/blog-post.svelte";

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
            const url = `https://localhost:6002/api/blog/search?query=${encodeURIComponent(searchQuery)}`;
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
    let timeout;
    let searchedPosts = Promise.resolve([]);
    let page = 1;
    let scrollElement;

    onMount(async () => {
        searchedPosts = filterResults(searchQuery);
    });

    const doSearch = debounce(
        () => {
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
                    const url = `https://localhost:6002/api/blog/search?query=${encodeURIComponent(searchQuery)}&page=${page}`;
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
        <div class="site-wrapper">
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
                {/if}
            </div>
        </div>
        <div class="search-summary">
            {#if totalResults}
      <span
      >{totalResults} articles
          {#if searchQuery}found{/if}</span
      >
            {/if}
        </div>
        {#if suggestions.length > 0 && lastPosts.length === 0}
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
    </div>
    <div class="padding-wrapper" bind:this={scrollElement} use:infiniteScroll>
        <div class="search-results">
            {#await searchedPosts}
                {#each lastPosts as post}
                    <BlogPost {post} {searchQuery}/>
                {/each}
            {:then posts}
                {#each posts as post}
                    <BlogPost {post} {searchQuery}/>
                {/each}
            {:catch error}
                <p style="color: rgb(128 128 128 / 80%)">The server is not responding.</p>
            {/await}
        </div>
    </div>
</div>

<style>
    .site-title {
        display: block;
        font-weight: 600;
        font-size: clamp(1.6rem, 6vw, 2.5rem);
        color: var(--accent-color-dim);
        cursor: default;
        letter-spacing: 0.1rem;
        white-space: nowrap;
        overflow: hidden;
    }

    .site-wrapper span {
        font-size: clamp(2.5rem, 8vw, 3.5rem);
        /*font-weight: 100;*/
        color: var(--accent-color-dimmer);
        /*-webkit-transform: scale(.8, 1); !* Safari and Chrome *!*/
        /*-moz-transform: scale(.8, 1); !* Firefox *!*/
        /*-ms-transform: scale(.8, 1); !* IE 9 *!*/
        /*-o-transform: scale(.8, 1); !* Opera *!*/
        /*transform: scale(.8, 1);*/
        /*align-self: flex-start;*/
        margin: 1rem 0;
        line-height: 4rem;
        font-family: Tahoma, sans-serif;
        cursor: default;
        -webkit-touch-callout: none;
        -webkit-user-select: none;
        -ms-user-select: none;
        user-select: none;
    }

    .site-wrapper {
        display: flex;
        align-items: center;
        gap: 0.5rem;
    }

    .search-statistics .dimmer {
        color: var(--accent-color-dimmer);
    }

    .site-logo {
        display: block;
        width: clamp(48px, 10vw, 64px);
        height: clamp(48px, 10vw, 64px);
        margin: 1rem 0;
    }


    .results-container {
        font-family: "Arial", sans-serif;
        border-radius: 5px;
        display: flex;
        flex-direction: column;
        height: 100vh;
        /*width: clamp(300px, 90%, 600px);*/
        margin: auto 0 auto 1rem;
    }

    .column-wrapper {
        display: inline-block;
        flex-direction: column;
        align-self: center;
        width: min-content;
    }

    .column-wrapper, .search-results {
        width: clamp(0rem, 100%, 600px);
    }

    /*.search-results {*/
    /*}*/

    .padding-wrapper {
        display: flex;
        flex-direction: column;
        overflow-y: auto;
        padding-bottom: 0.5rem;
        align-items: center;
        padding-left: 1rem;
        /*padding-left: clamp(0rem, calc(1rem - ((1200px - 100vw) / 100)), 1rem);*/
    }


    .padding-wrapper::-webkit-scrollbar {
        display: none;

    }


    .padding-wrapper::-webkit-scrollbar:vertical {
        display: block;
        background: transparent;
        /*margin: -10px;*/
        /*width: 0;*/
        /*height: 0;*/
        /*scrollbar-gutter: stable;*/
        /*padding: 0;*/

    }


    .padding-wrapper::-webkit-scrollbar-thumb {
        background: var(--accent-color-dimmer);
        border-radius: 1rem;
        border: 0.38rem solid transparent;
        background-clip: padding-box;
        /*margin: 0;*/
        /*scrollbar-gutter: stable;*/
        /*padding: 0;*/
    }

    .padding-wrapper::-webkit-scrollbar-thumb:hover {
        background: var(--accent-color-dim);
        border: 0.36rem solid transparent;
        background-clip: padding-box;
        /*margin: 0;*/
        /*scrollbar-gutter: stable;*/
        /*padding: 0;*/
    }

    .padding-wrapper::-webkit-scrollbar-track {
        /*box-shadow: inset 0 0 6px rgba(0, 0, 0, 0.3);*/
        border-radius: 1rem;
        /*border: solid 1px transparent;*/
        /*background: transparent;*/
    }

    .padding-wrapper::-webkit-scrollbar-track:hover {
        box-shadow: inset 0 0 6px rgba(0, 0, 0, 0.5);
        /*border-radius: 1rem;*/
        /*margin: 0;*/
    }


    input[type="search"]::-webkit-search-cancel-button {
        -webkit-appearance: none;
        height: 16px;
        width: 16px;
        margin-left: 0.4em;
        background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%23777'><path d='M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z'/></svg>");
        cursor: pointer;
    }

    input[type="search"] {
        -webkit-appearance: none;
        -moz-appearance: none;
        appearance: none;
        width: 100%;
        font-size: 1.25rem;
        padding: 1rem 1.5rem;
        border: none;
        border-radius: 0.5rem 1.5rem 0.5rem 1.5rem;
        background-color: var(--foreground-color);
        box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
    }

    ::placeholder {
        color: var(--accent-color-dim);
        opacity: 1;
    }

    ::-ms-input-placeholder {
        color: var(--accent-color-dim);
    }

    input[type="search"]:focus {
        border: none;
        outline: none;
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
        background-color: var(--foreground-color-bright);
    }

    .search-statistics {
        display: flex;
        justify-content: flex-end;
        margin: 0.25rem 0.25rem 0 0;
    }

    .search-statistics * {
        gap: 0.8rem;
        font-size: 0.8rem;
        cursor: default;
        color: var(--accent-color-dim);
    }

    .search-summary * {
        display: flex;
        justify-content: flex-start;
        margin: 6px;
        font-size: 0.9rem;
        color: var(--accent-color-dim);
    }

    .search-suggestions * {
        font-size: 1em;
        margin: 0.2em 0;
        display: inline-block;
    }


    @media (max-width: 650px) {
        .search-wrapper {
            padding-right: 1rem;
        }

        .padding-wrapper {
            padding-left: 0rem;
        }
    }

    @media (max-width: 600px) {
        /*.results-container {*/
        /*    width: 90%;*/
        /*}*/
        /*.search-results, .column-wrapper {*/
        /*    width: 100%;*/
        /*}*/
        .padding-wrapper::-webkit-scrollbar-track {
            box-shadow: none;
            border-radius: 1rem;


            /*background: transparent;*/
        }

        input[type="search"] {
            padding: 0.8rem 1.5rem;
        }


        /*.site-title {*/
        /*    flex: 1 1 auto; !* grow | shrink | basis *!*/
        /*    min-width: 0; !* Ensures text can shrink below its default minimum width *!*/
        /*}*/
        /*.site-logo {*/
        /*    !*display: block;*!*/
        /*    width: 48px;*/
        /*    height: 48px;*/
        /*    !*margin: 1rem 0;*!*/
        /*}*/
        /*.site-wrapper span {*/
        /*    !*font-size: 2.5rem;*!*/
        /*    margin: 1rem 0;*/
        /*    line-height: 3.5rem;*/
        /*    font-family: Tahoma, sans-serif;*/
        /*    cursor: default;*/
        /*    -webkit-touch-callout: none;*/
        /*    -webkit-user-select: none;*/
        /*    -ms-user-select: none;*/
        /*    user-select: none;*/
        /*}*/
    }
</style>
