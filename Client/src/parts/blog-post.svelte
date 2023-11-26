<script>
    import {baseUrl} from '../constants.js';

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
            <!--            <div class="blog-company">-->
            <!--{#if post.logoName && post.blogId}-->
            <!--    <img-->
            <!--            class="blog-logo"-->
            <!--            src={`https://localhost:6002/api/blog/${post.blogId}/image/${post.logoName}`}-->
            <!--            alt={post.company}-->
            <!--    />-->
            <!--{/if}{post.company}</div>-->
            <div class="blog-title" lang="en">
                <a href={post.link} target="_blank" rel="noopener noreferrer" lang="en"
                >{@html post.title}</a>
                <img
                        class="blog-logo"
                        src={`https://${baseUrl}/api/blog/${post.blogId}/image/${post.logoName}`}
                        alt={post.company}
                        loading="lazy"
                />
            </div>


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
        {#if authors}<span class="dimmer">|</span>
            <div class="authors">
                {authors}
            </div>
        {/if}
    </div>
</div>
