<script>
  import debounce from "just-debounce-it";
  import { onMount } from "svelte";
  import BlogPost from "../parts/blog-post.svelte";

  async function filterResults(search = "") {
    isLoading = true;

    try {
      // trim the search query
      search = search.trim();
      if (search !== "" && search === lastSearchQuery) {
        return lastPosts;
      }
      lastSearchQuery = search;
      let start = performance.now();
      const res = await fetch(
        "https://sparkify.dev/api/blog/search?query=" + search
      );
      let end = performance.now();
      totalDurationInMs = Math.round(end - start);

      const payload = await res.json();

      let wasCacheHit = false;
      if (res.status === 200) {
        const cacheControlHeader = res.headers.get("cache-control");
        if (cacheControlHeader && cacheControlHeader.includes("disk")) {
          wasCacheHit = true;
        }
      } else if (res.status === 304) {
        wasCacheHit = true;
      }
      const cfCacheStatus = res.headers.get("Cf-Cache-Status");
      if (cfCacheStatus === "HIT") {
        wasCacheHit = true;
      }

      if (payload.stats) {
        serverDurationInMs = wasCacheHit
          ? 0
          : Math.max(0, payload.stats.durationInMs);
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
            content: posts.content,
            blogId: posts.blogId,
            company: posts.company,
            logo: posts.logo,
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
  let searchQuery = "";
  let lastSearchQuery = "";
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

  const doSearch = debounce(
    () => {
      searchedPosts = filterResults(searchQuery);
    },
    50,
    true
  );
</script>

<div class="results-container">
  <img class="logo" src="./sparkify.png" width="64px" alt="Sparkify Logo" />
  <div id="search-wrapper">
    <input
      type="search"
      id="search"
      placeholder="Search for anything..."
      autocomplete="off"
      bind:value={searchQuery}
      on:input={doSearch}
    />
    <div class="search-statistics">
      {#if serverDurationInMs}
        <span style="color: rgb(128 128 128 / 80%)"
          >server: {serverDurationInMs} ms | round trip: {totalDurationInMs}
          ms</span
        >
      {/if}
    </div>
  </div>
  <div class="search-summary">
    {#if totalResults}
      <span style="color: rgb(128 128 128 / 80%)"
        >{totalResults} articles
        {#if searchQuery}matched{/if}</span
      >
    {/if}
  </div>
  {#if suggestions.length > 0}
    <div class="search-suggestions">
      <div>Did you mean?</div>
      <br />
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
        <br />
      {/each}
    </div>
  {/if}
  <div class="search-results">
    {#await searchedPosts}
      {#each lastPosts as post}
        <BlogPost {post} {searchQuery} />
      {/each}
    {:then posts}
      {#each posts as post}
        <BlogPost {post} {searchQuery} />
      {/each}
    {:catch error}
      <p style="color: rgb(128 128 128 / 80%)">The server is not responding.</p>
    {/await}
  </div>
</div>

<style>
  .logo {
    display: block;
    margin: 0 auto;
    margin-top: 1rem;
    margin-bottom: 1rem;
  }

  .results-container {
    width: 80%;
    max-width: 800px;
    margin: auto;
    font-family: "Arial", sans-serif;
    border-radius: 5px;
    display: flex;
    flex-direction: column;
    height: 100vh;
  }

  .search-results {
    display: flex;
    flex-direction: column;
    overflow-y: auto;
    margin: 0 0 1em 0;
  }

  .search-results::-webkit-scrollbar {
    display: none;
  }

  .search-results {
    scrollbar-width: none;
  }

  @media only screen and (max-width: 800px) {
    .results-container {
      width: 90%;
    }
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
    padding: 1rem;
    border: none;
    border-radius: 0.25rem;
    background-color: #343434;
    color: #ececec;
    box-shadow: 2px 4px 6px rgba(0, 0, 0, 0.4);
  }

  input[type="search"]:focus {
    border: none;
    outline: none;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.6);
    background-color: #363636;
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
    font-size: 1em;
    margin: 0.2em 0;
    display: inline-block;
  }
</style>
