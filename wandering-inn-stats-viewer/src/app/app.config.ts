import {ApplicationConfig} from '@angular/core';
import {provideRouter, withComponentInputBinding, withRouterConfig} from '@angular/router';
import {routes} from './app.routes';

// Zoneless (the default since Angular 21): change detection runs on signal changes, events and router updates.
export const appConfig: ApplicationConfig = {
  providers: [
    provideRouter(
      routes,
      // Route and query params are bound to the routed components' inputs; ":type" lives on the parent route.
      withComponentInputBinding(),
      withRouterConfig({paramsInheritanceStrategy: 'always'})
    ),
  ]
};
