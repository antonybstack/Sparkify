<script>
    import debounce from 'just-debounce-it';
    import {onMount} from 'svelte';
    import BlogPost from '../parts/blog-post.svelte';

    async function filterResults(search = "") {
        isLoading = true;

        try {
            // trim the search query
            search = search.trim();
            if (search !== "" && search === lastSearchQuery) {
                return lastPosts;
            }
            lastSearchQuery = search;
            // const res = await fetch("https://sparkify.dev/api/blog/search?query=" + search1);
            let start = performance.now();
            const res = await fetch("https://localhost/api/blog/search?query=" + search);
            let end = performance.now();
            totalDurationInMs = Math.round(end - start);

            const payload = (await res.json());
            if (payload.stats) {
                serverDurationInMs = Math.max(0, payload.stats.durationInMs);
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
                        date: new Date(posts.date),
                        categories: posts.categories,
                        content: posts.content,
                        blogId: posts.blogId,
                        company: posts.company,
                        logo: posts.logo
                    };
                });
            } else {
                suggestions = payload.data;
                lastPosts = [];
            }
        } catch (err) {
            console.log("Search request errored out: " + err);
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
    let searchQuery = '';
    let lastSearchQuery = '';
    let lastPosts = [];
    let serverDurationInMs = null;
    let totalDurationInMs = null;
    let totalResults = null;
    let suggestions = [];
    let timeout;
    let searchedPosts = Promise.resolve([]);

    onMount(async () => {
        searchedPosts = filterResults(searchQuery);
    });

    const doSearch = debounce(() => {
        searchedPosts = filterResults(searchQuery);
        // }, 50, true);
    }, 100, true);
</script>

<div class="results-container">
    <!--    https://github.com/OpenMined/PySyft/blob/8daa30a460b679585f4f6d0b9707bfc0110ca27a/packages/grid/frontend/src/routes/(app)/users/%2Bpage.svelte#L70-->
    <!--    <Search on:type={doSearch} bind:value={searchQuery}/>-->
    <div id="search-input">
        <input type="search"
               id="search"
               placeholder="Search for anything..."
               autocomplete="off"
               bind:value={searchQuery}
               on:input={doSearch}/>
        <div class="search-statistics">
            {#if serverDurationInMs}
                <span style="color: rgb(128 128 128 / 80%)">server: {serverDurationInMs} ms / total: {totalDurationInMs}
                    ms</span>
            {/if}
        </div>
    </div>
    <!--  duration  -->
    <!-- Total results -->
    <div class="search-summary">
        {#if totalResults}
            <span style="color: rgb(128 128 128 / 80%)">{totalResults} articles
                {#if searchQuery}matched{/if}</span>
        {/if}
    </div>
    {#if suggestions.length > 0}
        <div class="search-suggestions">

            <p>Did you mean?</p>
            {#each suggestions as suggestion}
                <a on:click={() => {
                    searchQuery = suggestion.title;
                        doSearch();
                }}>{suggestion.title}</a>
                <br>
            {/each}
        </div>
    {/if}
    {#await searchedPosts}
        {#each lastPosts as post}
            <BlogPost post={post}/>
        {/each}
    {:then posts}
        {#each posts as post}
            <BlogPost post={post}/>
        {/each}
    {:catch error}
        <p style="color: rgb(128 128 128 / 80%)">The server is not responding.</p>
    {/await}
</div>
<style>
    .results-container {
        width: 80%;
        max-width: 800px;
        margin: 20px auto;
        font-family: 'Arial', sans-serif;
        border-radius: 5px;
        padding: 20px;
        box-shadow: 0 2px 10px rgba(0, 0, 0, 0.1);
    }


    input[type="search"]::-webkit-search-cancel-button {
        -webkit-appearance: none;
        height: 16px;
        width: 16px;
        margin-left: .4em;
        background-image: url("data:image/svg+xml;utf8,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 24 24' fill='%23777'><path d='M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z'/></svg>");
        cursor: pointer;
    }

    input[type="search"] {
        -webkit-appearance: none; /* For Safari and Chrome */
        -moz-appearance: none; /* For Firefox */
        appearance: none;
        width: 100%;
        font-size: 1.25rem;
        padding: 0.5rem;
        border: none;
        border-bottom: 2px solid rgba(255, 255, 255, 0.2);
        border-radius: 0.25rem;
        /*transition: border 0.4s;*/
        background-color: #343434;
        color: #ececec;
    }

    #search-input {
        margin: 0.5rem;
    }

    input[type="search"]:focus {
        border: none;
        outline: none;
        box-shadow: 0 0 0 1px rgba(255, 255, 255, 0.1);
        border-bottom: 2px solid rgba(255, 255, 255, 0.4);
    }

    .search-statistics {
        display: flex;
        justify-content: flex-end;
        margin: 4px 0 0 0;
        gap: 0.8rem;
        font-size: 0.8rem;
    }

    .search-summary {
        display: flex;
        justify-content: flex-start;
        margin: 6px;
        font-size: 0.9rem;
    }

    .search-suggestions a {
        font-size: 1.2rem;
        margin: .2rem 0;
        display: inline-block;
    }
</style>
