import { Pipe, PipeTransform } from '@angular/core';

@Pipe({ name: 'teamFilter', standalone: true })
export class TeamFilterPipe implements PipeTransform {
  transform(members: any[], teamType: string): any[] {
    if (!members) return [];
    return members.filter(m => m.teamType === teamType);
  }
}