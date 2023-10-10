<script>
  export let post;
  export let searchQuery;
  export const authors = post.authors.join(", ");
  function horizontalScroll(node) {
    node.addEventListener("wheel", function (e) {
      if (e.deltaY !== 0) {
        node.scrollLeft += e.deltaY;
        e.preventDefault();
      }
    });

    return {
      destroy() {
        node.removeEventListener("wheel", horizontalScroll);
      },
    };
  }
</script>

<div class="blog-post">
  <div class="blog-header">
    {#if post.logo && post.blogId}
      <img
        class="blog-logo"
        src={`https://sparkify.dev/api/blog/${post.blogId}/image/${post.logo}`}
        alt={post.company}
      />
    {/if}
    <div class="blog-details">
      <h2 class="blog-title">
        <a href={post.link} target="_blank" rel="noopener noreferrer"
          >{@html post.title}</a
        >
      </h2>
      <div class="blog-company">{post.company}</div>
    </div>
  </div>
  {#if post.content}
    <p class="blog-content{searchQuery ? ' highlighted' : ''}">
      {@html post.content}
    </p>
  {/if}
  {#if post.categories.length > 0}
    <div class="blog-categories" use:horizontalScroll>
      {#each post.categories as category}
        <span>{category}</span>
      {/each}
    </div>
  {/if}
  <div class="blog-meta">
    <div class="blog-date">
      {post.date.toLocaleDateString(navigator.language, {
        weekday: "short",
        year: "numeric",
        month: "short",
        day: "numeric",
      })}
    </div>
    {#if authors}<span>|</span>
      <div class="authors">
        <div class="author-icon" />
        {authors}
      </div>
    {/if}
  </div>
</div>

<style>
  .blog-meta {
    display: flex;
    color: #888888;
    font-size: 12px;
    margin: 0 0 0 0.5em;
  }

  .authors {
    display: flex;
    align-self: center;
    white-space: nowrap;
    overflow: hidden;
  }

  .blog-date {
    align-self: center;
    white-space: nowrap;
  }

  .author-icon {
    margin-right: 0.75em;
    height: 0.8em;
    width: 0.8em;
    align-self: center;
    color: #eeeeee;
    background-image: url('data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512"><style>svg{fill:grey}<\/style><path d="M224 256A128 128 0 1 0 224 0a128 128 0 1 0 0 256zm-45.7 48C79.8 304 0 383.8 0 482.3C0 498.7 13.3 512 29.7 512H418.3c16.4 0 29.7-13.3 29.7-29.7C448 383.8 368.2 304 269.7 304H178.3z"/></svg>');
    background-size: cover;
  }

  .blog-content {
    color: rgba(255, 255, 255, 0.8);
    padding: 0.2em 0.8em;
    line-height: 1.6;
    margin: 0.75em auto;
    transition: all 0.2s ease-in-out;
    color: rgba(255, 255, 255, 0.8);
    box-shadow: inset 4px 4px 10px rgba(0, 0, 0, 0.1),
      inset -4px -4px 10px rgba(0, 0, 0, 0.2);
    background-color: rgba(0, 0, 0, 0.05);
    border-radius: 0.4em;
    display: block;
    display: -webkit-box;
    -webkit-line-clamp: 3;
    -webkit-box-orient: vertical;
    overflow: hidden;
    position: relative;
  }

  .blog-content:hover {
    box-shadow: inset 4px 4px 10px rgba(0, 0, 0, 0.3),
      inset -4px -4px 10px rgba(0, 0, 0, 0.25);
    cursor: pointer;
  }

  .blog-post {
    font-family: Arial, sans-serif;
    transition: box-shadow 0.2s;
    background-color: #2c2c2c;
    padding: 1.25em;
    margin-bottom: 1em;
    border-radius: 0.2em;
    box-shadow: 0 2px 5px rgba(0, 0, 0, 0.05);
  }

  .blog-post:hover {
    background-color: #2e2e2e;
  }

  .blog-post:last-child {
    margin-bottom: 0;
  }

  .blog-post:hover {
    box-shadow: 0 6px 12px rgba(0, 0, 0, 0.15);
  }

  .blog-header {
    display: flex;
    flex-direction: row;
    width: 100%;
  }

  .blog-details {
    display: flex;
    flex-direction: column;
    justify-content: flex-start;
    flex: 1;
    gap: 0.2em;
  }

  .blog-logo {
    height: 4em;
    width: 4em;
    margin-right: 1em;
    border-radius: 0.2em;
  }

  .blog-title {
    font-size: 24px;
    margin: 0;
  }

  .blog-company {
    font-size: 14px;
    color: #ffffff80;
  }

  .blog-meta {
    list-style-type: none;
    gap: 16px;
  }

  .blog-categories {
    overflow-x: auto;
    white-space: nowrap;
    display: flex;
    align-items: center;
    margin: 0.5em 0 0.5em 0;
  }

  .blog-categories span {
    display: inline-block;
    align-items: center;
    padding: 0.5em 1em;
    font-size: 12px;
    border-radius: 4px;
    background-color: rgba(255, 255, 255, 0.08);
    white-space: nowrap;
    transition: background-color 0.2s ease-in-out;
    margin-right: 0.5em;
  }

  .blog-categories span:last-child {
    margin-right: 0;
  }

  .blog-categories span:hover {
    background-color: rgb(255, 255, 255, 0.15);
    cursor: default;
  }

  .blog-categories::-webkit-scrollbar {
    display: none;
  }

  .blog-categories {
    scrollbar-width: none;
  }

  @media only screen and (max-width: 800px) {
    .blog-title {
      font-size: 20px;
    }

    .blog-logo {
      height: 3em;
      width: 3em;
      margin-right: 0.8em;
    }
  }
</style>
