<style>
    .loading {
        position: relative;
        background-color: var(--foreground-color);
        overflow: hidden;
    }

    .loading::after {
        display: block;
        content: "";
        position: absolute;
        width: 100%;
        transform: translateX(-100%);
        animation: 1s loading linear 0.2s infinite;
        background: linear-gradient(90deg, transparent, var(--foreground-color-bright), transparent);
        bottom: 0;
        left: 0;
        right: 0;
        top: 0;
        z-index: 1;
    }

    @keyframes loading {
        0% {
            transform: translateX(-100%);
        }
        40% {
            transform: translateX(100%);
        }
        100% {
            transform: translateX(100%);
        }
    }

    .loading .brighter::after {
        background: linear-gradient(90deg, transparent, var(--accent-color-dim-20), transparent);
    }

    .loading .brighter {
        background-color: var(--foreground-color-bright-25);
    }


    .skeleton-container {
        display: flex;
        justify-content: space-between;
        align-items: center;
    }

    .skeleton-header {
        width: 80%;
        height: 2rem;
        border-radius: 0.25rem;
        margin-bottom: 1em;
    }

    .skeleton-image {
        width: 2.5rem;
        height: 2.5rem;
        border-radius: 5rem;
        margin-top: -0.5em;
    }

    .skeleton-content {
        height: 1rem;
        margin: 0.75em 0;
    }

</style>
<div class="loading blog-post skeleton-wrapper" style="height: 175px;">
    <div class="skeleton-container">
        <div class="loading brighter skeleton-header " style="width:80%;"></div>
        <div class="loading brighter skeleton-image"></div>
    </div>
    <div class="loading brighter skeleton-content" style="width:75%;"></div>
    <div class="loading brighter skeleton-content" style="width:70%;"></div>
    <div class="loading brighter skeleton-content" style="width:78%;"></div>
    <div class="loading brighter skeleton-content" style="width:72%;"></div>
    <div class="loading brighter skeleton-subheader"></div>
</div>
