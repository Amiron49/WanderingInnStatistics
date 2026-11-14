import { Component, ChangeDetectionStrategy } from '@angular/core';
import { MatDrawerContainer, MatDrawer } from '@angular/material/sidenav';
import { AppNavigationComponent } from './app-navigation/app-navigation.component';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterOutlet } from '@angular/router';

@Component({
    selector: 'app-root',
    templateUrl: './app.component.html',
    styleUrls: ['./app.component.scss'],
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatDrawerContainer, MatDrawer, AppNavigationComponent, MatButton, MatIcon, RouterOutlet]
})
export class AppComponent {
}
