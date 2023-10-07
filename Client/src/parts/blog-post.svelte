<script>
    export let post;
</script>

<div class="blog-post">
    <div class="blog-header">
        {#if post.logo && post.blogId}
            <img class="blog-logo" src={`https://localhost/api/blog/${post.blogId}/image/${post.logo}`}
                 alt={post.company}>
        {/if}
        <div class="blog-details">
            <h2 class="blog-title">{@html post.title}</h2>
            <div class="blog-meta">
                <div>
                    <p class="blog-company">{post.company}</p>
                    <p class="blog-date">{post.date.toLocaleDateString(navigator.language, {
                        weekday: 'short',
                        year: 'numeric',
                        month: 'short',
                        day: 'numeric'
                    })}</p>
                </div>
                <div class="blog-categories">
                    {#if post.categories}
                        {#each post.categories as category}
                            <span>{category}</span>
                        {/each}
                    {/if}
                </div>
            </div>
        </div>
    </div>
    <!-- TODO: Authors   -->
    {#if post.content}
        <p class="blog-content">"...{@html post.content}..."</p>
        <a class="blog-readmore" href="{post.link}" target="_blank">read more</a>
    {/if}

</div>

<style>
    /*.blog-content {*/
    /*    color: #bebebe;*/
    /*    line-height: 1.5;*/
    /*    margin: 0;*/
    /*    display: inline;*/
    /*    !*overflow: hidden;*!*/
    /*    !*display: -webkit-box;*!*/
    /*    !*!*-webkit-line-clamp: 3;*!*!*/
    /*    !*!*line-clamp: 3;*!*!*/
    /*    !*!*-webkit-box-orient: vertical;*!*!*/
    /*}*/

    .blog-content {
        color: rgba(255, 255, 255, 0.8);
        font-style: italic;
        border-radius: 16px;
        padding: 20px;
        line-height: 1.6;
        box-shadow: inset 4px 4px 10px rgba(0, 0, 0, 0.1), inset -4px -4px 10px rgba(0, 0, 0, 0.2);
        max-width: 800px;
        margin: 20px auto;
        background-color: rgba(0, 0, 0, 0.05);
        transition: all 0.2s ease-in-out;
    }

    .blog-content:hover {
        box-shadow: inset 4px 4px 10px rgba(0, 0, 0, 0.3), inset -4px -4px 10px rgba(0, 0, 0, 0.25);
        cursor: pointer;
    }


    /*.highlighted {*/
    /*    background: rgba(0, 0, 0, 1);*/
    /*    padding: 2px 5px;*/
    /*    border-radius: 5px;*/
    /*}*/


    .blog-readmore {
        color: #57b3ff;
        font-size: 14px;
        text-decoration: none;
        display: block;
        text-align: right;
        margin: 6px 0 0 0;
        padding: 0;
    }

    .blog-readmore:hover {
        text-decoration: underline;
    }

    .blog-post {
        font-family: Arial, sans-serif;
        transition: box-shadow 0.2s;
        background-color: #2c2c2c;
        padding: 20px;
        margin-bottom: 15px;
        border-radius: 4px;
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

    .blog-title {
        font-size: 24px;
        margin: 0;
    }

    .blog-meta {
        display: flex;
        margin-top: 10px;
    }

    .blog-meta .blog-categories {
        align-items: start;

        list-style-type: none;
        padding: 0;
        margin: 0;
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
    }

    .blog-meta {
        list-style-type: none;
        gap: 16px;
    }

    .blog-meta p {
        list-style-type: none;
        margin: 0 0 4px 0;
    }

    .blog-date {
        color: #888888;
        font-size: 12px;
    }

    .blog-company {
        font-size: 16px;
        color: #d5d5d5;
    }

    .blog-categories {
        margin: 8px 0 0 0;
    }

    .blog-categories span {
        display: inline-block;
        align-items: center;
        padding: 4px 10px;
        font-size: 12px;
        border-radius: 4px;
        background-color: rgb(255, 255, 255, 0.08);
        color: #c2c2c2;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
        max-width: 180px;
        transition: background-color 0.2s ease-in-out;
    }

    .blog-categories span:hover {
        background-color: rgb(255, 255, 255, 0.15);
        cursor: default;
    }

    .blog-header {
        display: flex;
        align-items: start;
        margin-bottom: 10px;
    }

    .blog-logo {
        width: 60px;
        height: auto;
        margin-right: 15px;
    }

    .blog-details {
        flex: 1;
    }

    .blog-image-container {
        display: flex;
        justify-content: center;
        align-items: center;
        margin: 20px 0;
    }

    .blog-image {
        max-width: 100%;
        border-radius: 5px;
    }
</style>
