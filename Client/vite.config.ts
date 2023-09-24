import {sveltekit} from '@sveltejs/kit/vite';
import {defineConfig} from 'vite';

export default defineConfig({
    plugins: [sveltekit()],
    server: {
        host: '0.0.0.0',
        cors: true,
        port: 4000,
    },
});


/*
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';


export default defineConfig({
	plugins: [sveltekit(),mkcert()],
	server: {
		https: true,
	},
});

*/
