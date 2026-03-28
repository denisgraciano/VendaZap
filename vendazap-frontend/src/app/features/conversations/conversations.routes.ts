import { Routes } from '@angular/router';

export const CONVERSATIONS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./conversations.component').then((m) => m.ConversationsComponent),
  },
];
