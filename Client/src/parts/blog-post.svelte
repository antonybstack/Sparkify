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

        <div class="blog-details">
            <div class="blog-company">
                {#if post.logoName && post.blogId}
                    <img
                            class="blog-logo"
                            src={`https://localhost:6002/api/blog/${post.blogId}/image/${post.logoName}`}
                            alt={post.company}
                    />
                {/if}{post.company}</div>
            <div class="blog-title" lang="en">
                <a href={post.link} target="_blank" rel="noopener noreferrer" lang="en"
                >{@html post.title}</a
                >
            </div>


        </div>
    </div>
    {#if post.content}
        <p class="blog-content{searchQuery ? ' highlighted' : ''}">
            {@html post.content}
        </p>
    {:else}
        <br/>
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
        {#if authors}<span class="dimmer">|</span>
            <div class="authors">
                <div class="author-icon"/>
                {authors}
            </div>
        {/if}
    </div>
</div>

<style>
    .blog-meta {
        display: flex;
        font-size: 12px;
        margin: 0 0 0 0.5em;
        color: var(--accent-color-dim);
    }

    .blog-meta * {
        color: var(--accent-color-dim);
    }

    .blog-meta .dimmer {
        color: var(--accent-color-dimmer);
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
        color: var(--accent-color);
        background-image: url('data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 448 512"><style>svg{fill:rgb(135, 130, 120)}<\/style><path d="M224 256A128 128 0 1 0 224 0a128 128 0 1 0 0 256zm-45.7 48C79.8 304 0 383.8 0 482.3C0 498.7 13.3 512 29.7 512H418.3c16.4 0 29.7-13.3 29.7-29.7C448 383.8 368.2 304 269.7 304H178.3z"/></svg>');
        background-size: cover;
    }

    .blog-content {
        color: var(--text-color);
        padding: 0.1em 0.2em;
        line-height: 1.7;
        margin: 0.75em auto;
        transition: all 0.2s ease-in-out;
        /*box-shadow: inset 4px 4px 10px rgba(0, 0, 0, 0.1),*/
        /*inset -4px -4px 10px rgba(0, 0, 0, 0.2);*/
        /*background-color: rgba(0, 0, 0, 0.05);*/
        /*border-radius: 0.4em;*/
        display: block;
        display: -webkit-box;
        -webkit-line-clamp: 3;
        -webkit-box-orient: vertical;
        overflow: hidden;
        position: relative;
        font-weight: 300;
    }

    .blog-post {
        font-family: Arial, sans-serif;
        transition: box-shadow 0.2s;
        background-color: var(--foreground-color);
        padding: 1.25em;
        margin-bottom: 1em;
        border-radius: 0.2em;
        box-shadow: 0 2px 5px rgba(0, 0, 0, 0.05);
    }

    .blog-post:last-child {
        margin-bottom: 0;
    }

    .blog-post:hover {
        box-shadow: 4px 4px 12px rgba(0, 0, 0, 0.15),
        -4px 4px 12px rgba(0, 0, 0, 0.15);
        cursor: pointer;
        background-color: var(--foreground-color-bright);
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
        /*height: fit-content;*/
        /*width: 4em;*/
        /*margin-right: 1em;*/
        /*border-radius: 0.2em;*/

        max-width: 2.25em;
        max-height: 2.25em;
        margin-right: 0.8em;
        align-self: center;
    }

    /*.blog-title {*/
    /*    padding-bottom: 0.4rem;*/
    /*}*/

    .blog-title a {
        font-size: 1.5rem;
        font-weight: 500;
        margin: 0;
        /*white-space: normal !important;*/
        /*word-break: break-all;*/
        /*hyphens: auto;*/
    }

    .blog-company {
        display: flex;
        align-items: center;
        font-size: 0.9rem;
        color: var(--accent-color);
        margin-top: -0.5em;
        padding-bottom: 0.4rem;
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
        white-space: nowrap;
        transition: background-color 0.2s ease-in-out;
        margin-right: 0.5em;
        background-color: rgb(62, 62, 62);
        color: var(--accent-color);
    }

    .blog-categories span:last-child {
        margin-right: 0;
    }

    .blog-categories span:hover {
        background-color: rgb(70, 70, 70);
        cursor: default;
    }

    .blog-categories::-webkit-scrollbar {
        display: none;
    }

    .blog-categories {
        scrollbar-width: none;
    }

    @media (max-width: 600px) {
        .blog-title a {
            font-size: 1.3rem;
        }
    }

    @media (max-width: 400px) {
        .blog-title a {
            font-size: 1.1rem;
        }
    }
</style>
