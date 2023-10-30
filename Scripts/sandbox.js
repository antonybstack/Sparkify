import {createRequire} from "module";
import fetch from 'node-fetch';
import {Readability} from "@mozilla/readability";
import {JSDOM} from "jsdom";
import Parser from "rss-parser";

let parser = new Parser();

(async () => {
    let feed = await parser.parseURL('https://ravendb.net/news/feed');
    console.log(feed.title);
    console.log(feed.description);
    console.log(feed.lastBuildDate);
    console.log(feed.link);
    console.log(feed.feedUrl);
    console.log(feed.paginationLinks);
    let item = feed.items[0];
    console.log(item.title)
    console.log(item.guid)
    console.log(item.link)
    console.log(item.isoDate)
    console.log(item.pubDate)
    console.log(item.creator)
    console.log(item['dc:creator'])
    console.log(item.summary)
    console.log(item.categories)
    console.log(item.content)
    console.log(item.contentSnippet)
    console.log(item['content:encoded'])
    console.log(item['content:encodedSnippet'])
})();

const require = createRequire(import.meta.url);

// Function to fetch an HTML page and return the extracted content
async function fetchAndExtractContent(url) {
    try {
        // Fetching the HTML content from the URL
        const response = await fetch(url);
        const html = await response.text();

        // Using JSDOM to parse the fetched HTML
        const dom = new JSDOM(html, {url});
        const reader = new Readability(dom.window.document);

        // Extracting the main content using Readability
        return reader.parse();
    } catch (error) {
        console.error('Error fetching or parsing:', error);
        return null;
    }
}

const url = "https://discord.com/blog/maxjourney-pushing-discords-limits-with-a-million-plus-online-users-in-a-single-server";

// Fetch, extract, and log the content
fetchAndExtractContent(url).then(article => {
    if (article) {
        console.log('Title:', article.title);
        console.log('Content:', article.textContent.slice(0, 500)); // Output first 500 chars
    } else {
        console.log('No article found or an error occurred');
    }
});

const doc = new JSDOM("<body>Look at this cat: <img src='./cat.jpg'></body>", {
    url: "https://www.example.com/the-page-i-got-the-source-from"
});
let reader = new Readability(doc.window.document);
let article = reader.parse();
console.log(article.title);
