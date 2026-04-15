export interface User {
  id: number;
  email: string;
  name: string;
}

export interface AuthResponse {
  accessToken: string;
  user: User;
}

export interface Board {
  id: number;
  name: string;
  ownerId: number;
  createdAt: string;
}

export interface Label {
  id: number;
  boardId: number;
  name: string;
  color: string;
}

export interface ChecklistItem {
  id: number;
  checklistId: number;
  text: string;
  isDone: boolean;
  position: number;
}

export interface Checklist {
  id: number;
  cardId: number;
  title: string;
  items: ChecklistItem[];
}

export interface TimeEntry {
  id: number;
  cardId: number;
  userId: number;
  startedAt: string;
  endedAt: string | null;
  durationSeconds: number | null;
}

export interface Assignee {
  userId: number;
  name: string;
  email: string;
}

export interface BoardMember {
  userId: number;
  email: string;
  name: string;
  role: 'Owner' | 'Member';
  addedAt: string;
}

export interface Card {
  id: number;
  listId: number;
  title: string;
  description: string | null;
  position: number;
  dueDate: string | null;
  checklistTotal: number;
  checklistDone: number;
  totalLoggedSeconds: number;
  activeTimerStartedAt: string | null;
  labels: Label[];
  assignees: Assignee[];
}

export interface CardDetail extends Card {
  checklists: Checklist[];
  timeEntries: TimeEntry[];
}

export interface BoardList {
  id: number;
  boardId: number;
  name: string;
  position: number;
  cards: Card[];
}

export interface BoardDetail extends Board {
  lists: BoardList[];
  labels: Label[];
  members: BoardMember[];
}

export interface ActiveTimer {
  entryId: number;
  cardId: number;
  startedAt: string;
}
