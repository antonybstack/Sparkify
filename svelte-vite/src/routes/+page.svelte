<script>
    import debounce from 'just-debounce-it';
    import {onMount} from 'svelte';

    async function filterResults(search1 = "") {
        isLoading = true;

        try {
            const res = await fetch("https://sparkify.dev/api/blog/search?query=" + search1);
            const data = (await res.json()).filter(x => x);
            // check if posts is a list of objects or a list of strings
            if (!data || data.length === 0) {
                suggestions = [];
                lastPosts = [];
            } else if (data[0].id) {
                suggestions = [];
                lastPosts = data.map((post) => {
                    return {
                        id: post.id,
                        title: post.title,
                        link: post.link
                    };
                });

            } else {
                // combine list of strings into one string
                suggestions = data;
                lastPosts = [];
            }
        } catch (err) {
            console.log("Search request errored out: " + err);
            suggestions = [];
            lastPosts = [];
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
    let lastPosts = [];
    let suggestions = [];
    let timeout;
    let searchedPosts = Promise.resolve([]);

    onMount(async () => {
        searchedPosts = filterResults(searchQuery);
    });

    const doSearch = debounce(() => {
        searchedPosts = filterResults(searchQuery);
    }, 50, true);

</script>

<div class="results-container">
    <h1>Search</h1>
    <!--    https://github.com/OpenMined/PySyft/blob/8daa30a460b679585f4f6d0b9707bfc0110ca27a/packages/grid/frontend/src/routes/(app)/users/%2Bpage.svelte#L70-->
    <!--    <Search on:type={doSearch} bind:value={searchQuery}/>-->
    <div id="search-input-cont">
        <input type="search"
               id="search"
               placeholder="Search Everything..."
               autocomplete="off"
               bind:value={searchQuery}
               on:input={doSearch}/>
    </div>
    {#if suggestions.length > 0}
        <p>Did you mean?</p>
        {#each suggestions as suggestion}
            <a href="#" on:click={() => searchQuery = suggestion}>{suggestion.title}</a>
            <br>
        {/each}
    {/if}
    {#await searchedPosts}
        {#each lastPosts as post}
            <div class="result-item">
                <h2>{@html post.title}</h2>
                <!--{#if post.body}-->
                <!--    <p>{@html post.body}</p>-->
                <!--{/if}-->
                <!--{@html post.highlights[0]}-->
                <br>
                <a href="{post.link}">Read more</a>
            </div>
        {/each}
    {:then posts}
        {#each posts as post}
            <div class="result-item">
                <h2>{@html post.title}</h2>
                <!--{#if post.body}-->
                <!--    <p>{@html post.body}</p>-->
                <!--{/if}-->
                <!--{@html post.highlights[0]}-->
                <br>
                <a href="{post.link}">Read more</a>
            </div>
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

    h1 {
        font-size: 24px;
        margin-bottom: 20px;
    }

    form {
        position: relative;
        margin-bottom: 20px;
    }

    input[type="search"] {
        width: 100%;
        font-size: 1.25rem;
        padding: 0.75rem;
        margin: 0.5rem 0;
        border: 2px solid #e0e0e0;
        border-radius: 0.25rem;
        transition: border 0.4s;
        background-color: #343434;
        color: #ececec
    }

    input[type="search"]:focus {
        border: 2px solid #4285f4;
    }


    .result-item {
        background-color: #2c2c2c;
        padding: 15px;
        margin-bottom: 15px;
        border-radius: 4px;
        box-shadow: 0 2px 5px rgba(0, 0, 0, 0.05);
    }

    .result-item h2 {
        font-size: 20px;
        margin-bottom: 10px;
    }

    a {
        color: #4285f4;
        text-decoration: none;
        transition: color 0.3s;
    }

    a:hover {
        text-decoration: underline;
    }

    .result-item:hover {
        background-color: #343434;
    }

    .result-item:last-child {
        border-bottom: none;
    }

</style>
